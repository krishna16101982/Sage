using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Security;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    public class AuthFunctions : IAuthFunctions
    {
        private const string MenuSelect = "SELECT Code, Name, Category, MenuPath FROM dbo.Modules WHERE IsActive = 1 AND MenuPath IS NOT NULL ORDER BY SortOrder, Name";
        private const string UserColumns = "Id, PublicId, FirstName, LastName, Username, Email, PasswordHash, MustChangePassword, IsSuperAdmin, Status, FailedLoginCount, LockoutEndUtc, PermissionVersion";
        private const int SqlErrorTokenAlreadyUsed = 50001;
        private static string _dummyHash;
        private static readonly object _dummyHashLock = new object();

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly ITokenFunctions _tokenFunctions;
        private readonly IAuditFunctions _auditFunctions;
        private readonly IPasswordResetNotifier _passwordResetNotifier;
        private readonly JwtIssuerOptions _jwtIssuerOptions;
        private readonly AuthSettings _authSettings;

        /// <summary>
        /// Constructor
        /// </summary>
        public AuthFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, ITokenFunctions __tokenFunctions,
            IAuditFunctions __auditFunctions, IPasswordResetNotifier __passwordResetNotifier, IOptions<JwtIssuerOptions> __jwtIssuerOptions, IOptions<AuthSettings> __authSettings)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _tokenFunctions = __tokenFunctions;
            _auditFunctions = __auditFunctions;
            _passwordResetNotifier = __passwordResetNotifier;
            _jwtIssuerOptions = __jwtIssuerOptions.Value;
            _authSettings = __authSettings.Value;
        }

        #region Helpers

        /// <summary>A failed write inside a login/refresh/password flow is a system error: the caller's catch turns it into a 500.</summary>
        private static void EnsureOk((IFunctionReturn functionReturn, long retValue) _result)
        {
            if (!_result.functionReturn.Status) throw new InvalidOperationException(string.Join("; ", _result.functionReturn.Message));
        }

        private static IFunctionReturn Unauthorized(string _message, string _methodName)
            => FunctionReturn.Fail(Constants.ReturnMessageTypes.TokenError, _message, Constants.HttpStatusCodes.Unauthorized, _methodName);

        private static AdminUser MapUser(Microsoft.Data.SqlClient.SqlDataReader r) => new AdminUser
        {
            Id = r.GetInt64(0),
            PublicId = r.GetGuid(1),
            FirstName = r.GetString(2),
            LastName = r.GetString(3),
            Username = r.GetString(4),
            Email = r.GetString(5),
            PasswordHash = r.GetString(6),
            MustChangePassword = r.GetBoolean(7),
            IsSuperAdmin = r.GetBoolean(8),
            Status = r.GetString(9),
            FailedLoginCount = r.GetInt32(10),
            LockoutEndUtc = r.IsDBNull(11) ? (DateTime?)null : DateTime.SpecifyKind(r.GetDateTime(11), DateTimeKind.Utc),
            PermissionVersion = r.GetInt32(12)
        };

        /// <summary>Hash verified against unknown usernames so response time does not reveal whether the account exists.</summary>
        private string DummyHash()
        {
            if (_dummyHash == null)
            {
                lock (_dummyHashLock)
                {
                    if (_dummyHash == null)
                    {
                        _dummyHash = SecurityHash.HashPassword("dummy-password-for-timing-only", _authSettings.PasswordHashIterations);
                    }
                }
            }
            return _dummyHash;
        }

        // 'where' is always one of the fixed clauses below, never user input.
        private Task<AdminUser> QueryUserAsync(string _where, IEnumerable<SqlParameter> _parameters, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync($"SELECT TOP 1 {UserColumns} FROM dbo.AdminUsers WHERE IsDeleted = 0 AND {_where}", MapUser, _parameters, _ct);

        private Task<AdminUser> GetUserByLoginAsync(string _identifier, CancellationToken _ct) => QueryUserAsync("(Username = @i OR Email = @i)", new[] { P("@i", _identifier) }, _ct);
        private Task<AdminUser> GetUserByEmailAsync(string _email, CancellationToken _ct) => QueryUserAsync("Email = @e", new[] { P("@e", _email) }, _ct);
        private Task<AdminUser> GetUserByPublicIdAsync(Guid _publicId, CancellationToken _ct) => QueryUserAsync("PublicId = @p", new[] { P("@p", _publicId) }, _ct);
        private Task<AdminUser> GetUserByIdAsync(long _id, CancellationToken _ct) => QueryUserAsync("Id = @id", new[] { P("@id", _id) }, _ct);

        private async Task RecordLoginAsync(long? _userId, string _usernameAttempted, bool _isSuccess, string _failureReason, string _ipAddress, string _userAgent, CancellationToken _ct)
        {
            try
            {
                var _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" INSERT dbo.LoginHistories (UserId, UsernameAttempted, IsSuccess, FailureReason, IpAddress, UserAgent) ");
                _sqlQuery.Append(" VALUES (@u, @name, @ok, @reason, @ip, @ua) ");
                (IFunctionReturn _functionReturn, long _id) = await _databaseFunctions.ExecuteInsertAsync(_sqlQuery.ToString(), new[]
                {
                    P("@u", _userId), P("@name", Truncate(_usernameAttempted, 256)), P("@ok", _isSuccess), P("@reason", _failureReason),
                    P("@ip", Truncate(_ipAddress, 45)), P("@ua", Truncate(_userAgent, 500))
                }, "F:Auth:RecordLogin", null, _ct);
                if (!_functionReturn.Status) _loggerFunctions.Error("F:Auth:RecordLogin", "Login history write failed: " + string.Join("; ", _functionReturn.Message));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error("F:Auth:RecordLogin", "Login history write failed: " + ex.Message, ex);
            }
        }

        /// <summary>Create a refresh token. A null family starts a new family (new login).</summary>
        private async Task<(string raw, DateTime expiresAtUtc)> CreateRefreshTokenAsync(long _userId, Guid _familyId, string _ipAddress, string _userAgent, CancellationToken _ct)
        {
            string _raw = TokenGenerator.CreateSecureToken();
            DateTime _expires = DateTime.UtcNow.AddDays(_jwtIssuerOptions.RefreshTokenDays);
            var _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" INSERT dbo.RefreshTokens (UserId, TokenHash, FamilyId, ExpiresAtUtc, CreatedByIp, UserAgent) ");
            _sqlQuery.Append(" VALUES (@u, @h, @f, @exp, @ip, @ua) ");
            EnsureOk(await _databaseFunctions.ExecuteInsertAsync(_sqlQuery.ToString(), new[]
            {
                P("@u", _userId), P("@h", TokenGenerator.Hash(_raw)), P("@f", _familyId), Dt("@exp", _expires),
                P("@ip", Truncate(_ipAddress, 45)), P("@ua", Truncate(_userAgent, 500))
            }, "F:Auth:CreateRefreshToken", null, _ct));
            return (_raw, _expires);
        }

        private async Task RevokeFamilyAsync(Guid _familyId, string _reason, CancellationToken _ct)
        {
            var _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" UPDATE dbo.RefreshTokens SET RevokedAtUtc = @now, RevokedReason = @r ");
            _sqlQuery.Append(" WHERE FamilyId = @f AND RevokedAtUtc IS NULL ");
            EnsureOk(await _databaseFunctions.ExecuteUpdateAsync(_sqlQuery.ToString(),
                new[] { Dt("@now", DateTime.UtcNow), P("@r", _reason), P("@f", _familyId) }, "F:Auth:RevokeFamily", null, _ct));
        }

        private async Task<CurrentUserResponse> BuildCurrentUserAsync(AdminUser _user, CancellationToken _ct)
        {
            // The procedure applies the Super Admin rule (all active permissions) itself.
            var _rows = await _databaseFunctions.QueryProcedureAsync(SP.AdminUserGetEffectivePermissions,
                r => (Permission: r.GetString(0), ModuleCode: r.GetString(1), ModuleName: r.GetString(2)),
                new[] { P("@UserId", _user.Id) }, _ct);

            var _permissions = _rows.Select(x => x.Permission).Distinct().ToList();
            var _granted = new HashSet<string>(_permissions, StringComparer.OrdinalIgnoreCase);
            var _menuModules = await _databaseFunctions.QueryAsync(MenuSelect,
                r => new MenuItem { Code = r.GetString(0), Name = r.GetString(1), Category = r.IsDBNull(2) ? null : r.GetString(2), Path = r.GetString(3) }, null, _ct);

            return new CurrentUserResponse
            {
                Id = _user.PublicId,
                Username = _user.Username,
                Email = _user.Email,
                FirstName = _user.FirstName,
                LastName = _user.LastName,
                IsSuperAdmin = _user.IsSuperAdmin,
                MustChangePassword = _user.MustChangePassword,
                Permissions = _permissions,
                // A module is in the sidebar when it has a link and the user may view it (the effective permissions already
                // hold every active permission for a Super Admin and leave out inactive modules).
                Menu = _menuModules.Where(m => _granted.Contains(m.Code + ".View")).ToList(),
                Modules = _rows.GroupBy(x => x.ModuleCode).Select(g => new ModuleItem { Code = g.Key, Name = g.First().ModuleName }).ToList()
            };
        }

        private async Task<AuthResult> BuildAuthResultAsync(AdminUser _user, string _rawRefresh, DateTime _refreshExpires, CancellationToken _ct)
        {
            (string _accessToken, int _expiresIn) = _tokenFunctions.CreateAccessToken(_user);
            return new AuthResult
            {
                RefreshToken = _rawRefresh,
                RefreshTokenExpiresAtUtc = _refreshExpires,
                Response = new LoginResponse { AccessToken = _accessToken, ExpiresIn = _expiresIn, User = await BuildCurrentUserAsync(_user, _ct) }
            };
        }

        /// <summary>
        /// Atomically set a new password, invalidate outstanding reset tokens and refresh tokens, and bump the permission version
        /// so existing access tokens stop working immediately.
        /// </summary>
        private async Task SetPasswordAsync(long _userId, string _newHash, string _revokeReason, CancellationToken _ct)
        {
            EnsureOk(await _databaseFunctions.ExecuteProcedureWriteAsync(SP.AdminUserSetPassword,
                new[] { P("@UserId", _userId), P("@PasswordHash", _newHash), Dt("@NowUtc", DateTime.UtcNow), P("@RevokeReason", _revokeReason) }, "F:Auth:SetPassword", null, _ct));
        }

        #endregion

        public async Task<(AuthResult authResult, IFunctionReturn functionReturn)> LoginAsync(HttpLoginPostParams _params, string _ipAddress, string _userAgent, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Auth:Login";
            #endregion
            try
            {
                string _identifier = (_params?.Username ?? string.Empty).Trim();
                string _password = _params?.Password ?? string.Empty;
                if (_identifier.Length == 0 || _password.Length == 0)
                {
                    return (null, FunctionReturn.Fail(Constants.ReturnMessageTypes.ValidationError, "Username and password are required.", Constants.HttpStatusCodes.BadRequest, _methodName));
                }

                DateTime _now = DateTime.UtcNow;
                AdminUser _user = await GetUserByLoginAsync(_identifier, _ct);

                if (_user == null)
                {
                    SecurityHash.VerifyHashedPassword(DummyHash(), _password); // equalize timing
                    await RecordLoginAsync(null, _identifier, false, "UnknownUser", _ipAddress, _userAgent, _ct);
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidCredentials, _methodName));
                }

                if (_user.Status != ApplicationDatabaseConstants.Status.Active)
                {
                    SecurityHash.VerifyHashedPassword(DummyHash(), _password);
                    await RecordLoginAsync(_user.Id, _identifier, false, _user.Status, _ipAddress, _userAgent, _ct);
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidCredentials, _methodName));
                }

                if (_user.LockoutEndUtc.HasValue && _user.LockoutEndUtc.Value > _now)
                {
                    // The password is not checked while locked, so a lock cannot be used to keep guessing.
                    await RecordLoginAsync(_user.Id, _identifier, false, "Locked", _ipAddress, _userAgent, _ct);
                    return (null, FunctionReturn.Fail(Constants.ReturnMessageTypes.AppError, ApplicationConstants.Auth.AccountLocked, Constants.HttpStatusCodes.TooManyRequests, _methodName));
                }

                if (!SecurityHash.VerifyHashedPassword(_user.PasswordHash, _password))
                {
                    // Count the failure; at the limit the procedure starts the lockout and resets the counter.
                    EnsureOk(await _databaseFunctions.ExecuteProcedureWriteAsync(SP.AdminUserRecordFailedLogin, new[]
                    {
                        P("@UserId", _user.Id), P("@MaxFailedAttempts", _authSettings.MaxFailedLoginAttempts), Dt("@LockoutEndUtc", _now.AddMinutes(_authSettings.LockoutMinutes))
                    }, _methodName, null, _ct));
                    await RecordLoginAsync(_user.Id, _identifier, false, "BadPassword", _ipAddress, _userAgent, _ct);
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidCredentials, _methodName));
                }

                // success: reset counters, upgrade the hash if the policy has been raised
                bool _rehash = SecurityHash.NeedsRehash(_user.PasswordHash, _authSettings.PasswordHashIterations);
                EnsureOk(await _databaseFunctions.ExecuteProcedureWriteAsync(SP.AdminUserRecordSuccessfulLogin, new[]
                {
                    P("@UserId", _user.Id), Dt("@NowUtc", _now), P("@NewPasswordHash", _rehash ? SecurityHash.HashPassword(_password, _authSettings.PasswordHashIterations) : null)
                }, _methodName, null, _ct));

                (string _rawRefresh, DateTime _refreshExpires) = await CreateRefreshTokenAsync(_user.Id, Guid.NewGuid(), _ipAddress, _userAgent, _ct);
                await RecordLoginAsync(_user.Id, _identifier, true, null, _ipAddress, _userAgent, _ct);
                await _auditFunctions.LogAsync(_user.Id, ApplicationEnumeration.AuditAction.Login.ToString(), ApplicationEnumeration.ObjectType.AdminUser.ToString(), _user.PublicId.ToString(), null, null, _ipAddress, _ct);

                return (await BuildAuthResultAsync(_user, _rawRefresh, _refreshExpires, _ct), new FunctionReturn { MethodName = _methodName });
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
                if (string.IsNullOrWhiteSpace(_rawRefreshToken))
                {
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));
                }

                var _token = await _databaseFunctions.QuerySingleOrDefaultAsync(
                    "SELECT Id, UserId, FamilyId, ExpiresAtUtc, RevokedAtUtc FROM dbo.RefreshTokens WHERE TokenHash = @h",
                    r => (Id: r.GetInt64(0), UserId: r.GetInt64(1), FamilyId: r.GetGuid(2), Expires: DateTime.SpecifyKind(r.GetDateTime(3), DateTimeKind.Utc), Revoked: !r.IsDBNull(4)),
                    new[] { P("@h", TokenGenerator.Hash(_rawRefreshToken)) }, _ct);

                if (_token.Id == 0)
                {
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));
                }
                if (_token.Revoked)
                {
                    // A rotated/revoked token is being replayed: assume theft and kill the whole family.
                    await RevokeFamilyAsync(_token.FamilyId, "reuse_detected", _ct);
                    _loggerFunctions.Warning(_methodName, $"Refresh token reuse detected for user id {_token.UserId}; family revoked.");
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));
                }
                if (_token.Expires <= DateTime.UtcNow)
                {
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));
                }

                AdminUser _user = await GetUserByIdAsync(_token.UserId, _ct);
                if (_user == null || _user.Status != ApplicationDatabaseConstants.Status.Active)
                {
                    await RevokeFamilyAsync(_token.FamilyId, "user_inactive", _ct);
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));
                }

                // Rotate: new token in the same family; the old one can be used exactly once.
                string _newRaw = TokenGenerator.CreateSecureToken();
                DateTime _newExpires = DateTime.UtcNow.AddDays(_jwtIssuerOptions.RefreshTokenDays);
                try
                {
                    await _databaseFunctions.ExecuteProcedureScalarAsync<long>(SP.RefreshTokenRotate, new[]
                    {
                        P("@OldTokenId", _token.Id), P("@UserId", _user.Id), P("@TokenHash", TokenGenerator.Hash(_newRaw)), P("@FamilyId", _token.FamilyId),
                        Dt("@ExpiresAtUtc", _newExpires), Dt("@NowUtc", DateTime.UtcNow), P("@CreatedByIp", Truncate(_ipAddress, 45)), P("@UserAgent", Truncate(_userAgent, 500))
                    }, _ct);
                }
                catch (DatabaseException ex) when (ex.SqlErrorNumber == SqlErrorTokenAlreadyUsed)
                {
                    // Lost a race with a concurrent refresh using the same token.
                    return (null, Unauthorized(ApplicationConstants.Auth.InvalidRefreshToken, _methodName));
                }

                return (await BuildAuthResultAsync(_user, _newRaw, _newExpires, _ct), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<IFunctionReturn> LogoutAsync(string _rawRefreshToken, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Auth:Logout";
            try
            {
                if (!string.IsNullOrWhiteSpace(_rawRefreshToken))
                {
                    var _token = await _databaseFunctions.QuerySingleOrDefaultAsync(
                        "SELECT UserId, FamilyId FROM dbo.RefreshTokens WHERE TokenHash = @h",
                        r => (UserId: r.GetInt64(0), FamilyId: r.GetGuid(1)),
                        new[] { P("@h", TokenGenerator.Hash(_rawRefreshToken)) }, _ct);
                    if (_token.UserId != 0)
                    {
                        await RevokeFamilyAsync(_token.FamilyId, "logout", _ct);
                        await _auditFunctions.LogAsync(_token.UserId, ApplicationEnumeration.AuditAction.Logout.ToString(), ApplicationEnumeration.ObjectType.AdminUser.ToString(), null, null, null, _ipAddress, _ct);
                    }
                }
                return CommonFunctions.AppSuccess(ApplicationConstants.Auth.LoggedOut, _methodName);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemError(_methodName);
            }
        }

        public async Task<IFunctionReturn> ForgotPasswordAsync(string _email, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Auth:ForgotPassword";
            try
            {
                string _normalized = (_email ?? string.Empty).Trim();
                if (ValidationFunctions.IsValidEmail(_normalized))
                {
                    AdminUser _user = await GetUserByEmailAsync(_normalized, _ct);
                    if (_user != null && _user.Status == ApplicationDatabaseConstants.Status.Active)
                    {
                        string _raw = TokenGenerator.CreateSecureToken();
                        DateTime _now = DateTime.UtcNow;
                        DateTime _expires = _now.AddMinutes(_authSettings.PasswordResetMinutes);
                        // Invalidates the user's older reset tokens and inserts the new one in one transaction.
                        EnsureOk(await _databaseFunctions.ExecuteProcedureWriteAsync(SP.PasswordResetTokenCreate,
                            new[] { P("@UserId", _user.Id), P("@TokenHash", TokenGenerator.Hash(_raw)), Dt("@ExpiresAtUtc", _expires), Dt("@NowUtc", _now), P("@RequestedIp", Truncate(_ipAddress, 45)) }, _methodName, null, _ct));
                        await _passwordResetNotifier.SendAsync(_user.Email, _user.FirstName, _raw, _expires, _ct);
                        await _auditFunctions.LogAsync(_user.Id, ApplicationEnumeration.AuditAction.PasswordReset.ToString() + "Requested", ApplicationEnumeration.ObjectType.AdminUser.ToString(), _user.PublicId.ToString(), null, null, _ipAddress, _ct);
                    }
                }
                // Same answer whether or not the account exists (no account enumeration).
                return CommonFunctions.AppSuccess(ApplicationConstants.Auth.ForgotPasswordGeneric, _methodName);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemError(_methodName);
            }
        }

        public async Task<IFunctionReturn> ResetPasswordAsync(HttpResetPasswordPostParams _params, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Auth:ResetPassword";
            try
            {
                IFunctionReturn _validation = ApplicationValidationFunctions.ValidateNewPassword(_params?.NewPassword, _params?.ConfirmPassword, _methodName);
                if (!_validation.Status) return _validation;
                if (string.IsNullOrWhiteSpace(_params.Token))
                {
                    return CommonFunctions.AppError(ApplicationConstants.Auth.InvalidResetToken, _methodName);
                }

                long _userId = await _databaseFunctions.ExecuteScalarAsync<long>(
                    @"SELECT t.UserId FROM dbo.PasswordResetTokens t JOIN dbo.AdminUsers u ON u.Id = t.UserId
WHERE t.TokenHash = @h AND t.UsedAtUtc IS NULL AND t.ExpiresAtUtc > @now AND u.IsDeleted = 0 AND u.Status = 'Active'",
                    new[] { P("@h", TokenGenerator.Hash(_params.Token)), Dt("@now", DateTime.UtcNow) }, _ct);
                if (_userId == 0)
                {
                    return CommonFunctions.AppError(ApplicationConstants.Auth.InvalidResetToken, _methodName);
                }

                await SetPasswordAsync(_userId, SecurityHash.HashPassword(_params.NewPassword, _authSettings.PasswordHashIterations), "password_reset", _ct);
                await _auditFunctions.LogAsync(_userId, ApplicationEnumeration.AuditAction.PasswordReset.ToString(), ApplicationEnumeration.ObjectType.AdminUser.ToString(), null, null, null, _ipAddress, _ct);
                return CommonFunctions.AppSuccess(ApplicationConstants.Auth.PasswordResetDone, _methodName);
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
                IFunctionReturn _validation = ApplicationValidationFunctions.ValidateNewPassword(_params?.NewPassword, _params?.ConfirmPassword, _methodName);
                if (!_validation.Status) return _validation;

                AdminUser _user = await GetUserByPublicIdAsync(_userPublicId, _ct);
                if (_user == null || _user.Status != ApplicationDatabaseConstants.Status.Active)
                {
                    return Unauthorized(Constants.TokenMessages.UnAuthorizedAccess, _methodName);
                }
                if (!SecurityHash.VerifyHashedPassword(_user.PasswordHash, _params.CurrentPassword ?? string.Empty))
                {
                    return CommonFunctions.AppError(ApplicationConstants.Auth.CurrentPasswordIncorrect, _methodName);
                }
                if (_params.CurrentPassword == _params.NewPassword)
                {
                    return CommonFunctions.AppError(ApplicationConstants.Auth.NewPasswordSameAsCurrent, _methodName);
                }

                await SetPasswordAsync(_user.Id, SecurityHash.HashPassword(_params.NewPassword, _authSettings.PasswordHashIterations), "password_changed", _ct);
                await _auditFunctions.LogAsync(_user.Id, ApplicationEnumeration.AuditAction.PasswordChange.ToString(), ApplicationEnumeration.ObjectType.AdminUser.ToString(), _user.PublicId.ToString(), null, null, _ipAddress, _ct);
                return CommonFunctions.AppSuccess(ApplicationConstants.Auth.PasswordChanged, _methodName);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemError(_methodName);
            }
        }

        public async Task<(CurrentUserResponse currentUser, IFunctionReturn functionReturn)> GetCurrentUserAsync(Guid _userPublicId, CancellationToken _ct = default)
        {
            string _methodName = "F:Auth:GetCurrentUser";
            try
            {
                AdminUser _user = await GetUserByPublicIdAsync(_userPublicId, _ct);
                if (_user == null || _user.Status != ApplicationDatabaseConstants.Status.Active)
                {
                    return (null, Unauthorized(Constants.TokenMessages.UnAuthorizedAccess, _methodName));
                }
                return (await BuildCurrentUserAsync(_user, _ct), new FunctionReturn { MethodName = _methodName });
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
                "SELECT COUNT(*) FROM dbo.AdminUsers WHERE PublicId = @p AND IsDeleted = 0 AND Status = 'Active' AND PermissionVersion = @pv",
                new[] { P("@p", _userPublicId), P("@pv", _permissionVersion) }, _ct);
            return _count == 1;
        }
    }
}
