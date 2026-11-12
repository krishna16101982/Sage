using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Security;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.AppValidations;
using SageUserApi.Globals;
using SageUserApi.Models;
using SageUserApi.Models.HttpPost;
using SP = SageUserApi.Globals.ApplicationDatabaseConstants.StoredProcedures;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// User Panel sign-in for sales users: login with lockout, refresh-token rotation with reuse detection, logout,
    /// change password and "who am I". Same rules as the Admin API, against dbo.SalesUsers and its own token tables.
    /// </summary>
    public class AuthFunctions : IAuthFunctions
    {
        private const string UserSelect = @"SELECT TOP 1 u.Id, u.PublicId, u.FirstName, u.LastName, u.Username, u.Email, u.PasswordHash, u.MustChangePassword,
    u.Role, u.Status, u.LockoutEndUtc, u.PermissionVersion, CASE WHEN m.Id IS NULL THEN NULL ELSE CONCAT(m.FirstName, N' ', m.LastName) END
FROM dbo.SalesUsers u LEFT JOIN dbo.SalesUsers m ON m.Id = u.ManagerId
WHERE u.IsDeleted = 0 AND ";
        private const int SqlErrorTokenAlreadyUsed = 50001;
        private static string _dummyHash;
        private static readonly object _dummyHashLock = new object();

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly ITokenFunctions _tokenFunctions;
        private readonly JwtIssuerOptions _jwtIssuerOptions;
        private readonly AuthSettings _authSettings;

        public AuthFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, ITokenFunctions __tokenFunctions,
            IOptions<JwtIssuerOptions> __jwtIssuerOptions, IOptions<AuthSettings> __authSettings)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _tokenFunctions = __tokenFunctions;
            _jwtIssuerOptions = __jwtIssuerOptions.Value;
            _authSettings = __authSettings.Value;
        }

        #region Helpers

        private static SqlParameter P(string _name, object _value) => new SqlParameter(_name, _value ?? DBNull.Value);
        private static SqlParameter Dt(string _name, DateTime _value) => new SqlParameter(_name, SqlDbType.DateTime2) { Value = _value };
        private static string Truncate(string _value, int _max) => string.IsNullOrEmpty(_value) || _value.Length <= _max ? _value : _value.Substring(0, _max);

        /// <summary>A failed write inside a login/refresh/password flow is a system error: the caller's catch turns it into a 500.</summary>
        private static void EnsureOk((IFunctionReturn functionReturn, long retValue) _result)
        {
            if (!_result.functionReturn.Status) throw new InvalidOperationException(string.Join("; ", _result.functionReturn.Message));
        }

        private static IFunctionReturn Unauthorized(string _message, string _methodName)
            => FunctionReturn.Fail(Constants.ReturnMessageTypes.TokenError, _message, Constants.HttpStatusCodes.Unauthorized, _methodName);

        private static SalesUser MapUser(SqlDataReader r) => new SalesUser
        {
            Id = r.GetInt64(0),
            PublicId = r.GetGuid(1),
            FirstName = r.GetString(2),
            LastName = r.GetString(3),
            Username = r.GetString(4),
            Email = r.GetString(5),
            PasswordHash = r.GetString(6),
            MustChangePassword = r.GetBoolean(7),
            Role = r.GetString(8),
            Status = r.GetString(9),
            LockoutEndUtc = r.IsDBNull(10) ? null : DateTime.SpecifyKind(r.GetDateTime(10), DateTimeKind.Utc),
            PermissionVersion = r.GetInt32(11),
            ManagerName = r.IsDBNull(12) ? null : r.GetString(12)
        };

        /// <summary>Hash verified against unknown usernames so response time does not reveal whether the account exists.</summary>
        private string DummyHash()
        {
            if (_dummyHash == null)
            {
                lock (_dummyHashLock)
                {
                    _dummyHash ??= SecurityHash.HashPassword("dummy-password-for-timing-only", _authSettings.PasswordHashIterations);
                }
            }
            return _dummyHash;
        }

        // The where clause is always one of the fixed strings below, never user input.
        private Task<SalesUser> QueryUserAsync(string _where, SqlParameter _parameter, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync(UserSelect + _where, MapUser, new[] { _parameter }, _ct);

        private Task<SalesUser> GetUserByLoginAsync(string _identifier, CancellationToken _ct) => QueryUserAsync("(u.Username = @i OR u.Email = @i)", P("@i", _identifier), _ct);
        private Task<SalesUser> GetUserByPublicIdAsync(Guid _publicId, CancellationToken _ct) => QueryUserAsync("u.PublicId = @p", P("@p", _publicId), _ct);
        private Task<SalesUser> GetUserByIdAsync(long _id, CancellationToken _ct) => QueryUserAsync("u.Id = @id", P("@id", _id), _ct);

        private async Task RecordAttemptAsync(long? _userId, string _username, bool _isSuccess, string _reason, string _ip, string _userAgent, CancellationToken _ct)
        {
            try
            {
                (IFunctionReturn _functionReturn, long _) = await _databaseFunctions.ExecuteInsertAsync(
                    "INSERT dbo.SalesUserLoginHistories (SalesUserId, UsernameAttempted, IsSuccess, FailureReason, IpAddress, UserAgent) VALUES (@u, @name, @ok, @reason, @ip, @ua)",
                    new[] { P("@u", _userId), P("@name", Truncate(_username, 256)), P("@ok", _isSuccess), P("@reason", _reason), P("@ip", Truncate(_ip, 45)), P("@ua", Truncate(_userAgent, 500)) },
                    "F:Auth:RecordAttempt", null, _ct);
                if (!_functionReturn.Status) _loggerFunctions.Error("F:Auth:RecordAttempt", "Login history write failed: " + string.Join("; ", _functionReturn.Message));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error("F:Auth:RecordAttempt", "Login history write failed: " + ex.Message, ex);
            }
        }

        /// <summary>Audit row for the sales user's own actions (ActorUserId is NULL: it points at admin users only).</summary>
        private async Task AuditAsync(string _action, SalesUser _user, string _ip, CancellationToken _ct)
        {
            try
            {
                await _databaseFunctions.ExecuteInsertAsync(
                    "INSERT dbo.AuditLogs (ActorUserId, Action, EntityType, EntityId, NewValues, IpAddress) VALUES (NULL, @a, 'SalesUser', @e, @n, @ip)",
                    new[] { P("@a", _action), P("@e", _user.PublicId.ToString()), P("@n", JsonSerializer.Serialize(new { By = "self", _user.Username })), P("@ip", Truncate(_ip, 45)) },
                    "F:Auth:Audit", null, _ct);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error("F:Auth:Audit", "Audit write failed: " + ex.Message, ex);
            }
        }

        private async Task<(string raw, DateTime expires)> CreateRefreshTokenAsync(long _userId, string _ip, string _userAgent, CancellationToken _ct)
        {
            string _raw = TokenGenerator.CreateSecureToken();
            DateTime _expires = DateTime.UtcNow.AddDays(_jwtIssuerOptions.RefreshTokenDays);
            EnsureOk(await _databaseFunctions.ExecuteInsertAsync(
                "INSERT dbo.SalesUserRefreshTokens (SalesUserId, TokenHash, FamilyId, ExpiresAtUtc, CreatedByIp, UserAgent) VALUES (@u, @h, @f, @exp, @ip, @ua)",
                new[] { P("@u", _userId), P("@h", TokenGenerator.Hash(_raw)), P("@f", Guid.NewGuid()), Dt("@exp", _expires), P("@ip", Truncate(_ip, 45)), P("@ua", Truncate(_userAgent, 500)) },
                "F:Auth:CreateRefreshToken", null, _ct));
            return (_raw, _expires);
        }

        private async Task RevokeFamilyAsync(Guid _familyId, string _reason, CancellationToken _ct)
        {
            EnsureOk(await _databaseFunctions.ExecuteUpdateAsync(
                "UPDATE dbo.SalesUserRefreshTokens SET RevokedAtUtc = @now, RevokedReason = @r WHERE FamilyId = @f AND RevokedAtUtc IS NULL",
                new[] { Dt("@now", DateTime.UtcNow), P("@r", _reason), P("@f", _familyId) }, "F:Auth:RevokeFamily", null, _ct));
        }

        private static CurrentSalesUserResponse ToResponse(SalesUser _user) => new CurrentSalesUserResponse
        {
            Id = _user.PublicId,
            Username = _user.Username,
            Email = _user.Email,
            FirstName = _user.FirstName,
            LastName = _user.LastName,
            Role = _user.Role,
            IsManager = _user.Role == ApplicationDatabaseConstants.SalesRoles.SalesManager,
            ManagerName = _user.ManagerName,
            MustChangePassword = _user.MustChangePassword
        };

        private AuthResult BuildAuthResult(SalesUser _user, string _rawRefresh, DateTime _refreshExpires)
        {
            (string _accessToken, int _expiresIn) = _tokenFunctions.CreateAccessToken(_user);
            return new AuthResult
            {
                RefreshToken = _rawRefresh,
                RefreshTokenExpiresAtUtc = _refreshExpires,
                Response = new LoginResponse { AccessToken = _accessToken, ExpiresIn = _expiresIn, User = ToResponse(_user) }
            };
        }

        #endregion

        public async Task<(AuthResult authResult, IFunctionReturn functionReturn)> LoginAsync(HttpLoginPostParams _params, string _ipAddress, string _userAgent, CancellationToken _ct = default)
        {
            string _methodName = "F:Auth:Login";
            try
            {
                string _identifier = (_params?.Username ?? string.Empty).Trim();
                string _password = _params?.Password ?? string.Empty;
                if (_identifier.Length == 0 || _password.Length == 0)
                    return (null, FunctionReturn.Fail(Constants.ReturnMessageTypes.ValidationError, "Username and password are required.", Constants.HttpStatusCodes.BadRequest, _methodName));

                DateTime _now = DateTime.UtcNow;
                SalesUser _user = await GetUserByLoginAsync(_identifier, _ct);

                if (_user == null)
                {
                    SecurityHash.VerifyHashedPassword(DummyHash(), _password); // equalize timing
                    await RecordAttemptAsync(null, _identifier, false, "UnknownUser", _ipAddress, _userAgent, _ct);
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidCredentials, _methodName));
                }
                if (_user.Status != ApplicationDatabaseConstants.Status.Active)
                {
                    SecurityHash.VerifyHashedPassword(DummyHash(), _password);
                    await RecordAttemptAsync(_user.Id, _identifier, false, _user.Status, _ipAddress, _userAgent, _ct);
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidCredentials, _methodName));
                }
                if (_user.LockoutEndUtc.HasValue && _user.LockoutEndUtc.Value > _now)
                {
                    // The password is not checked while locked, so a lock cannot be used to keep guessing.
                    await RecordAttemptAsync(_user.Id, _identifier, false, "Locked", _ipAddress, _userAgent, _ct);
                    return (null, FunctionReturn.Fail(Constants.ReturnMessageTypes.AppError, ApplicationConstants.Auth.AccountLocked, Constants.HttpStatusCodes.TooManyRequests, _methodName));
                }
                if (!SecurityHash.VerifyHashedPassword(_user.PasswordHash, _password))
                {
                    EnsureOk(await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SalesUserRecordLogin, new[]
                    {
                        P("@SalesUserId", _user.Id), P("@IsSuccess", false), Dt("@NowUtc", _now),
                        P("@MaxFailedAttempts", _authSettings.MaxFailedLoginAttempts), Dt("@LockoutEndUtc", _now.AddMinutes(_authSettings.LockoutMinutes))
                    }, _methodName, null, _ct));
                    await RecordAttemptAsync(_user.Id, _identifier, false, "BadPassword", _ipAddress, _userAgent, _ct);
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidCredentials, _methodName));
                }

                bool _rehash = SecurityHash.NeedsRehash(_user.PasswordHash, _authSettings.PasswordHashIterations);
                EnsureOk(await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SalesUserRecordLogin, new[]
                {
                    P("@SalesUserId", _user.Id), P("@IsSuccess", true), Dt("@NowUtc", _now),
                    P("@NewPasswordHash", _rehash ? SecurityHash.HashPassword(_password, _authSettings.PasswordHashIterations) : null)
                }, _methodName, null, _ct));

                (string _raw, DateTime _expires) = await CreateRefreshTokenAsync(_user.Id, _ipAddress, _userAgent, _ct);
                await RecordAttemptAsync(_user.Id, _identifier, true, null, _ipAddress, _userAgent, _ct);
                return (BuildAuthResult(_user, _raw, _expires), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(AuthResult authResult, IFunctionReturn functionReturn)> RefreshAsync(string _rawRefreshToken, string _ipAddress, string _userAgent, CancellationToken _ct = default)
        {
            string _methodName = "F:Auth:Refresh";
            try
            {
                if (string.IsNullOrWhiteSpace(_rawRefreshToken)) return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));

                var _token = await _databaseFunctions.QuerySingleOrDefaultAsync(
                    "SELECT Id, SalesUserId, FamilyId, ExpiresAtUtc, RevokedAtUtc FROM dbo.SalesUserRefreshTokens WHERE TokenHash = @h",
                    r => (Id: r.GetInt64(0), UserId: r.GetInt64(1), FamilyId: r.GetGuid(2), Expires: DateTime.SpecifyKind(r.GetDateTime(3), DateTimeKind.Utc), Revoked: !r.IsDBNull(4)),
                    new[] { P("@h", TokenGenerator.Hash(_rawRefreshToken)) }, _ct);

                if (_token.Id == 0) return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));
                if (_token.Revoked)
                {
                    // A rotated/revoked token is being replayed: assume theft and end the whole family.
                    await RevokeFamilyAsync(_token.FamilyId, "reuse_detected", _ct);
                    _loggerFunctions.Warning(_methodName, $"Refresh token reuse detected for sales user id {_token.UserId}; family revoked.");
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));
                }
                if (_token.Expires <= DateTime.UtcNow) return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));

                SalesUser _user = await GetUserByIdAsync(_token.UserId, _ct);
                if (_user == null || _user.Status != ApplicationDatabaseConstants.Status.Active)
                {
                    await RevokeFamilyAsync(_token.FamilyId, "user_inactive", _ct);
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));
                }

                string _newRaw = TokenGenerator.CreateSecureToken();
                DateTime _newExpires = DateTime.UtcNow.AddDays(_jwtIssuerOptions.RefreshTokenDays);
                try
                {
                    await _databaseFunctions.ExecuteProcedureScalarAsync<long>(SP.SalesUserRefreshTokenRotate, new[]
                    {
                        P("@OldTokenId", _token.Id), P("@SalesUserId", _user.Id), P("@TokenHash", TokenGenerator.Hash(_newRaw)), P("@FamilyId", _token.FamilyId),
                        Dt("@ExpiresAtUtc", _newExpires), Dt("@NowUtc", DateTime.UtcNow), P("@CreatedByIp", Truncate(_ipAddress, 45)), P("@UserAgent", Truncate(_userAgent, 500))
                    }, _ct);
                }
                catch (DatabaseException ex) when (ex.SqlErrorNumber == SqlErrorTokenAlreadyUsed)
                {
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName)); // lost a race with a concurrent refresh
                }
                return (BuildAuthResult(_user, _newRaw, _newExpires), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<IFunctionReturn> LogoutAsync(string _rawRefreshToken, CancellationToken _ct = default)
        {
            string _methodName = "F:Auth:Logout";
            try
            {
                if (!string.IsNullOrWhiteSpace(_rawRefreshToken))
                {
                    Guid _familyId = await _databaseFunctions.ExecuteScalarAsync<Guid>("SELECT FamilyId FROM dbo.SalesUserRefreshTokens WHERE TokenHash = @h",
                        new[] { P("@h", TokenGenerator.Hash(_rawRefreshToken)) }, _ct);
                    if (_familyId != Guid.Empty) await RevokeFamilyAsync(_familyId, "logout", _ct);
                }
                return CommonFunctions.AppSuccess(ApplicationConstants.Auth.LoggedOut, _methodName);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemError(_methodName);
            }
        }

        public async Task<IFunctionReturn> ChangePasswordAsync(Guid _userPublicId, HttpChangePasswordPostParams _params, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Auth:ChangePassword";
            try
            {
                var _errors = new List<string>(ValidationFunctions.CheckPasswordStrength(_params?.NewPassword));
                if (_params?.NewPassword != _params?.ConfirmPassword) _errors.Add(ApplicationConstants.Auth.PasswordMismatch);
                if (_errors.Count > 0)
                    return new FunctionReturn { Status = false, MessageType = Constants.ReturnMessageTypes.ValidationError, Message = _errors, HttpStatusCode = Constants.HttpStatusCodes.BadRequest, MethodName = _methodName };

                SalesUser _user = await GetUserByPublicIdAsync(_userPublicId, _ct);
                if (_user == null || _user.Status != ApplicationDatabaseConstants.Status.Active) return Unauthorized(Constants.TokenMessages.UnAuthorizedAccess, _methodName);
                if (!SecurityHash.VerifyHashedPassword(_user.PasswordHash, _params.CurrentPassword ?? string.Empty))
                    return CommonFunctions.AppError(ApplicationConstants.Auth.CurrentPasswordIncorrect, _methodName);
                if (_params.CurrentPassword == _params.NewPassword)
                    return CommonFunctions.AppError(ApplicationConstants.Auth.NewPasswordSameAsCurrent, _methodName);

                EnsureOk(await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SalesUserChangeOwnPassword, new[]
                {
                    P("@SalesUserId", _user.Id), P("@PasswordHash", SecurityHash.HashPassword(_params.NewPassword, _authSettings.PasswordHashIterations)), Dt("@NowUtc", DateTime.UtcNow)
                }, _methodName, null, _ct));
                await AuditAsync("PasswordChange", _user, _ipAddress, _ct);
                return CommonFunctions.AppSuccess(ApplicationConstants.Auth.PasswordChanged, _methodName);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemError(_methodName);
            }
        }

        public async Task<(CurrentSalesUserResponse currentUser, IFunctionReturn functionReturn)> GetCurrentUserAsync(Guid _userPublicId, CancellationToken _ct = default)
        {
            string _methodName = "F:Auth:GetCurrentUser";
            try
            {
                SalesUser _user = await GetUserByPublicIdAsync(_userPublicId, _ct);
                if (_user == null || _user.Status != ApplicationDatabaseConstants.Status.Active)
                    return (null, Unauthorized(Constants.TokenMessages.UnAuthorizedAccess, _methodName));
                return (ToResponse(_user), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<bool> IsAccessTokenUserValidAsync(Guid _userPublicId, int _permissionVersion, CancellationToken _ct = default)
        {
            int _count = await _databaseFunctions.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.SalesUsers WHERE PublicId = @p AND IsDeleted = 0 AND Status = 'Active' AND PermissionVersion = @pv",
                new[] { P("@p", _userPublicId), P("@pv", _permissionVersion) }, _ct);
            return _count == 1;
        }
    }
}
