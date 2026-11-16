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
using SageAdminApi.Models.Filter;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions;
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

        /// <summary>One sales user row (from GetRowAsync or dbo.usp_SalesUser_GetPaged; both use these column names).</summary>
        private static SalesUserDto MapSalesUser(DataRow _row)
        {
            DateTime? _lockoutEnd = _row.GetNullableUtc("LockoutEndUtc");
            return new SalesUserDto
            {
                Id = _row.GetGuid("PublicId"),
                FirstName = _row.GetText("FirstName"),
                LastName = _row.GetText("LastName"),
                Username = _row.GetText("Username"),
                Email = _row.GetText("Email"),
                MobileNumber = _row.GetText("MobileNumber"),
                Role = _row.GetText("Role"),
                Status = _row.GetText("Status"),
                ManagerId = _row.GetNullableGuid("ManagerPublicId"),
                ManagerName = _row.GetText("ManagerName"),
                TeamSize = _row.GetInt("TeamSize"),
                MustChangePassword = _row.GetBool("MustChangePassword"),
                IsLockedOut = _lockoutEnd.HasValue && _lockoutEnd.Value > DateTime.UtcNow,
                LastLoginAtUtc = _row.GetNullableUtc("LastLoginAtUtc"),
                CreatedAtUtc = _row.GetUtc("CreatedAtUtc")
            };
        }

        /// <summary>One sales user (not deleted) by PublicId, with their manager and team size, or null.</summary>
        private async Task<SalesUserDto> GetRowAsync(Guid _id, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT u.PublicId, u.FirstName, u.LastName, u.Username, u.Email, u.MobileNumber, u.Role, u.Status, ");
            _sqlQuery.Append("        m.PublicId AS ManagerPublicId, CASE WHEN m.Id IS NULL THEN NULL ELSE CONCAT(m.FirstName, N' ', m.LastName) END AS ManagerName, ");
            _sqlQuery.Append("        (SELECT COUNT(*) FROM dbo.SalesUsers x WHERE x.ManagerId = u.Id AND x.IsDeleted = 0) AS TeamSize, ");
            _sqlQuery.Append("        u.MustChangePassword, u.LockoutEndUtc, u.LastLoginAtUtc, u.CreatedAtUtc ");
            _sqlQuery.Append(" FROM dbo.SalesUsers u ");
            _sqlQuery.Append(" LEFT JOIN dbo.SalesUsers m ON m.Id = u.ManagerId ");
            _sqlQuery.Append(" WHERE u.PublicId = @id AND u.IsDeleted = 0 ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@id", _id) }, "F:SalesUser:GetRow", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            return _row == null ? null : MapSalesUser(_row);
        }

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
                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.SalesUserGetPaged, new[]
                {
                    P("@Search", _search), P("@Role", _role), P("@Status", _status), P("@ManagerPublicId", _filter.ManagerId),
                    P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection), P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                return (new PagedResult<SalesUserDto>
                {
                    Items = _dataTable.MapRows(MapSalesUser),
                    Page = _paging.Page,
                    PageSize = _paging.PageSize,
                    TotalCount = OutInt(_totalCount)
                }, _functionReturn);
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
                // Active sales managers, for the "Manager" drop-down.
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT u.PublicId, CONCAT(u.FirstName, N' ', u.LastName) AS Name, u.Username, ");
                _sqlQuery.Append("        (SELECT COUNT(*) FROM dbo.SalesUsers x WHERE x.ManagerId = u.Id AND x.IsDeleted = 0) AS TeamSize ");
                _sqlQuery.Append(" FROM dbo.SalesUsers u ");
                _sqlQuery.Append(" WHERE u.Role = 'SalesManager' AND u.Status = 'Active' AND u.IsDeleted = 0 ");
                _sqlQuery.Append(" ORDER BY u.FirstName, u.LastName ");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), null, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                return (_dataTable.MapRows(_row => new SalesManagerOption
                {
                    Id = _row.GetGuid("PublicId"),
                    Name = _row.GetText("Name"),
                    Username = _row.GetText("Username"),
                    TeamSize = _row.GetInt("TeamSize")
                }), _functionReturn);
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
