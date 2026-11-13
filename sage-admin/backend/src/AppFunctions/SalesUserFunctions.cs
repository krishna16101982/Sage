using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.Filter;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SageCommonLib.Security;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using Roles = SageAdminApi.Globals.ApplicationDatabaseConstants.SalesRoles;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// User Panel accounts (sales persons and sales managers), managed by admins: list, create, edit (role/manager),
    /// activate/deactivate, set a temporary password. Writes go through stored procedures that enforce the role rules.
    /// </summary>
    public class SalesUserFunctions : ISalesUserFunctions
    {
        private static readonly string[] SortKeys = { "name", "username", "email", "role", "status", "manager", "createdAtUtc", "lastLoginAtUtc" };

        /// <summary>Same columns, in the same order, as dbo.usp_SalesUser_GetPaged.</summary>
        private const string SalesUserSelect = @"SELECT u.PublicId, u.FirstName, u.LastName, u.Username, u.Email, u.MobileNumber, u.Role, u.Status,
    m.PublicId, CASE WHEN m.Id IS NULL THEN NULL ELSE CONCAT(m.FirstName, N' ', m.LastName) END,
    (SELECT COUNT(*) FROM dbo.SalesUsers x WHERE x.ManagerId = u.Id AND x.IsDeleted = 0),
    u.MustChangePassword, u.LockoutEndUtc, u.LastLoginAtUtc, u.CreatedAtUtc
FROM dbo.SalesUsers u LEFT JOIN dbo.SalesUsers m ON m.Id = u.ManagerId
WHERE u.PublicId = @id AND u.IsDeleted = 0";

        private const string ManagersSelect = @"SELECT u.PublicId, CONCAT(u.FirstName, N' ', u.LastName), u.Username,
    (SELECT COUNT(*) FROM dbo.SalesUsers x WHERE x.ManagerId = u.Id AND x.IsDeleted = 0)
FROM dbo.SalesUsers u
WHERE u.Role = 'SalesManager' AND u.Status = 'Active' AND u.IsDeleted = 0
ORDER BY u.FirstName, u.LastName";

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly IAuditFunctions _auditFunctions;
        private readonly AuthSettings _authSettings;

        public SalesUserFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IAuditFunctions __auditFunctions, IOptions<AuthSettings> __authSettings)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _auditFunctions = __auditFunctions;
            _authSettings = __authSettings.Value;
        }

        #region Helpers

        private static SalesUserDto MapSalesUser(SqlDataReader r)
        {
            DateTime? _lockoutEnd = Utc(r, 12);
            return new SalesUserDto
            {
                Id = r.GetGuid(0),
                FirstName = r.GetString(1),
                LastName = r.GetString(2),
                Username = r.GetString(3),
                Email = r.GetString(4),
                MobileNumber = r.IsDBNull(5) ? null : r.GetString(5),
                Role = r.GetString(6),
                Status = r.GetString(7),
                ManagerId = r.IsDBNull(8) ? null : r.GetGuid(8),
                ManagerName = r.IsDBNull(9) ? null : r.GetString(9),
                TeamSize = r.GetInt32(10),
                MustChangePassword = r.GetBoolean(11),
                IsLockedOut = _lockoutEnd.HasValue && _lockoutEnd.Value > DateTime.UtcNow,
                LastLoginAtUtc = Utc(r, 13),
                CreatedAtUtc = Utc(r, 14).Value
            };
        }

        private Task<SalesUserDto> GetRowAsync(Guid _id, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync(SalesUserSelect, MapSalesUser, new[] { P("@id", _id) }, _ct);

        /// <summary>Audit snapshot: profile and role fields only, never password data.</summary>
        private static object Snapshot(SalesUserDto u) => u == null ? null : new { u.FirstName, u.LastName, u.Username, u.Email, u.MobileNumber, u.Role, u.ManagerId, u.ManagerName, u.Status };

        private Task AuditAsync(long? _actorUserId, ApplicationEnumeration.AuditAction _action, Guid _id, object _old, object _new, string _ipAddress, CancellationToken _ct)
            => _auditFunctions.LogAsync(_actorUserId, _action.ToString(), ApplicationEnumeration.ObjectType.SalesUser.ToString(), _id.ToString(), _old, _new, _ipAddress, _ct);

        private static FunctionReturn Ok(string _methodName, string _message, string _httpStatus = Constants.HttpStatusCodes.Ok)
            => new FunctionReturn { MethodName = _methodName, HttpStatusCode = _httpStatus, Message = new List<string> { _message } };

        #endregion

        public async Task<(PagedResult<SalesUserDto> result, IFunctionReturn functionReturn)> GetSalesUsersAsync(SalesUserFilter _filter, CancellationToken _ct = default)
        {
            string _methodName = "F:SalesUser:GetSalesUsers";
            try
            {
                _filter = _filter ?? new SalesUserFilter();
                PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortKeys, "name");
                if (string.IsNullOrWhiteSpace(_filter.SortDirection)) _paging.SortDirection = "ASC";

                string _role = _filter.Role == Roles.SalesPerson || _filter.Role == Roles.SalesManager ? _filter.Role : null;
                string _status = _filter.Status == ApplicationDatabaseConstants.Status.Active || _filter.Status == ApplicationDatabaseConstants.Status.Inactive
                    || _filter.Status == ApplicationDatabaseConstants.Status.Locked ? _filter.Status : null;
                string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
                var _totalCount = IntOut("@TotalCount");
                var _parameters = new List<SqlParameter>
                {
                    P("@Search", _search), P("@Role", _role), P("@Status", _status), P("@ManagerPublicId", _filter.ManagerId),
                    P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection), P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                };
                IReadOnlyList<SalesUserDto> _items = await _databaseFunctions.QueryProcedureAsync(SP.SalesUserGetPaged, MapSalesUser, _parameters, _ct);
                int _total = OutInt(_totalCount);

                return (new PagedResult<SalesUserDto> { Items = _items, Page = _paging.Page, PageSize = _paging.PageSize, TotalCount = _total }, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SalesUserDto user, IFunctionReturn functionReturn)> GetSalesUserAsync(Guid _id, CancellationToken _ct = default)
        {
            string _methodName = "F:SalesUser:GetSalesUser";
            try
            {
                SalesUserDto _user = await GetRowAsync(_id, _ct);
                if (_user == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.SalesUserNotFound, _methodName));
                return (_user, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(List<SalesManagerOption> managers, IFunctionReturn functionReturn)> GetManagersAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:SalesUser:GetManagers";
            try
            {
                var _managers = await _databaseFunctions.QueryAsync(ManagersSelect,
                    r => new SalesManagerOption { Id = r.GetGuid(0), Name = r.GetString(1), Username = r.GetString(2), TeamSize = r.GetInt32(3) }, null, _ct);
                return (_managers.ToList(), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SalesUserDto user, IFunctionReturn functionReturn)> CreateSalesUserAsync(HttpSalesUserPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:SalesUser:CreateSalesUser";
            IFunctionReturn _functionReturn;
            Guid _id;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateSalesUser(_params?.FirstName, _params?.LastName, _params?.Username?.Trim(), true, _params?.Email?.Trim(),
                    _params?.MobileNumber?.Trim(), _params?.Role, _params?.ManagerId, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);
                _functionReturn = ApplicationValidationFunctions.ValidateNewPassword(_params.Password, _params.ConfirmPassword, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                // The procedure checks duplicates and the manager (active SalesManager) under one lock.
                try
                {
                    _id = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.SalesUserCreate, new[]
                    {
                        P("@FirstName", Clean(_params.FirstName)), P("@LastName", Clean(_params.LastName)), P("@Username", _params.Username.Trim()),
                        P("@Email", _params.Email.Trim()), P("@MobileNumber", Clean(_params.MobileNumber)),
                        P("@PasswordHash", SecurityHash.HashPassword(_params.Password, _authSettings.PasswordHashIterations)),
                        P("@Role", _params.Role), P("@ManagerPublicId", _params.ManagerId), P("@ActorUserId", _actorUserId)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                SalesUserDto _created = await GetRowAsync(_id, _ct);
                await AuditAsync(_actorUserId, ApplicationEnumeration.AuditAction.Create, _id, null, Snapshot(_created), _ipAddress, _ct);
                return (_created, Ok(_methodName, "Sales user created.", Constants.HttpStatusCodes.Created));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SalesUserDto user, IFunctionReturn functionReturn)> UpdateSalesUserAsync(Guid _id, HttpSalesUserPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:SalesUser:UpdateSalesUser";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateSalesUser(_params?.FirstName, _params?.LastName, null, false, _params?.Email?.Trim(),
                    _params?.MobileNumber?.Trim(), _params?.Role, _params?.ManagerId, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                SalesUserDto _existing = await GetRowAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.SalesUserNotFound, _methodName));

                // The procedure enforces the role rules (manager with a team cannot become a sales person, etc.).
                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SalesUserUpdate, new[]
                {
                    P("@PublicId", _id), P("@FirstName", Clean(_params.FirstName)), P("@LastName", Clean(_params.LastName)), P("@Email", _params.Email.Trim()),
                    P("@MobileNumber", Clean(_params.MobileNumber)), P("@Role", _params.Role), P("@ManagerPublicId", _params.ManagerId), P("@ActorUserId", _actorUserId)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                SalesUserDto _updated = await GetRowAsync(_id, _ct);
                await AuditAsync(_actorUserId, ApplicationEnumeration.AuditAction.Update, _id, Snapshot(_existing), Snapshot(_updated), _ipAddress, _ct);
                return (_updated, Ok(_methodName, "Sales user updated."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SalesUserDto user, IFunctionReturn functionReturn)> SetStatusAsync(Guid _id, HttpSalesUserStatusParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:SalesUser:SetStatus";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                string _status = _params?.Status;
                if (_status != ApplicationDatabaseConstants.Status.Active && _status != ApplicationDatabaseConstants.Status.Inactive)
                    return (null, ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.SalesStatusInvalid, _methodName));

                SalesUserDto _existing = await GetRowAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.SalesUserNotFound, _methodName));

                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SalesUserSetStatus,
                    new[] { P("@PublicId", _id), P("@Status", _status), P("@ActorUserId", _actorUserId) }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                SalesUserDto _updated = await GetRowAsync(_id, _ct);
                await AuditAsync(_actorUserId, ApplicationEnumeration.AuditAction.StatusChange, _id,
                    new { _existing.Status, _existing.IsLockedOut }, new { _updated.Status, _updated.IsLockedOut }, _ipAddress, _ct);
                return (_updated, Ok(_methodName, _status == ApplicationDatabaseConstants.Status.Active ? "Sales user activated." : "Sales user deactivated."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<IFunctionReturn> ResetPasswordAsync(Guid _id, HttpSalesUserPasswordParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:SalesUser:ResetPassword";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateNewPassword(_params?.NewPassword, _params?.ConfirmPassword, _methodName);
                if (!_functionReturn.Status) return _functionReturn;

                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SalesUserResetPassword, new[]
                {
                    P("@PublicId", _id), P("@PasswordHash", SecurityHash.HashPassword(_params.NewPassword, _authSettings.PasswordHashIterations)), P("@ActorUserId", _actorUserId)
                }, _methodName, ApplicationConstants.ValidationMessages.PasswordResetBySalesAdmin, _ct);
                if (!_functionReturn.Status) return _functionReturn;

                await AuditAsync(_actorUserId, ApplicationEnumeration.AuditAction.PasswordReset, _id, null, null, _ipAddress, _ct);
                return _functionReturn;
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemError(_methodName);
            }
        }
    }
}
