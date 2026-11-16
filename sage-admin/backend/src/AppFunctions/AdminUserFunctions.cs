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
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Admin Panel accounts: list, create (with group and temporary password), edit, activate/deactivate, set a temporary
    /// password. Super Admin accounts can only be changed by a Super Admin; nobody can deactivate themselves.
    /// </summary>
    public class AdminUserFunctions : IAdminUserFunctions
    {
        private static readonly string[] SortKeys = { "name", "username", "email", "status", "group", "createdAtUtc", "lastLoginAtUtc" };

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly IAuditFunctions _auditFunctions;
        private readonly AuthSettings _authSettings;

        public AdminUserFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IAuditFunctions __auditFunctions, IOptions<AuthSettings> __authSettings)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _auditFunctions = __auditFunctions;
            _authSettings = __authSettings.Value;
        }

        #region Helpers

        /// <summary>One admin user row (from GetRowAsync or dbo.usp_AdminUser_GetPaged; both use these column names).</summary>
        private static AdminUserDto MapAdminUser(DataRow _row)
        {
            DateTime? _lockoutEnd = _row.GetNullableUtc("LockoutEndUtc");
            return new AdminUserDto
            {
                Id = _row.GetGuid("PublicId"),
                FirstName = _row.GetText("FirstName"),
                LastName = _row.GetText("LastName"),
                Username = _row.GetText("Username"),
                Email = _row.GetText("Email"),
                MobileNumber = _row.GetText("MobileNumber"),
                Status = _row.GetText("Status"),
                IsSuperAdmin = _row.GetBool("IsSuperAdmin"),
                GroupId = _row.GetNullableGuid("GroupPublicId"),
                GroupName = _row.GetText("GroupName"),
                GroupIsActive = _row.GetText("GroupStatus") == ApplicationDatabaseConstants.Status.Active,
                MustChangePassword = _row.GetBool("MustChangePassword"),
                IsLockedOut = _lockoutEnd.HasValue && _lockoutEnd.Value > DateTime.UtcNow,
                LastLoginAtUtc = _row.GetNullableUtc("LastLoginAtUtc"),
                CreatedAtUtc = _row.GetUtc("CreatedAtUtc")
            };
        }

        /// <summary>One admin user (not deleted) by PublicId, with their group, or null.</summary>
        private async Task<AdminUserDto> GetRowAsync(Guid _id, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT u.PublicId, u.FirstName, u.LastName, u.Username, u.Email, u.MobileNumber, u.Status, u.IsSuperAdmin, ");
            _sqlQuery.Append("        g.PublicId AS GroupPublicId, g.Name AS GroupName, g.Status AS GroupStatus, ");
            _sqlQuery.Append("        u.MustChangePassword, u.LockoutEndUtc, u.LastLoginAtUtc, u.CreatedAtUtc ");
            _sqlQuery.Append(" FROM dbo.AdminUsers u ");
            _sqlQuery.Append(" LEFT JOIN dbo.AdminUserGroups ug ON ug.UserId = u.Id AND ug.IsPrimary = 1 ");
            _sqlQuery.Append(" LEFT JOIN dbo.PermissionGroups g ON g.Id = ug.GroupId ");
            _sqlQuery.Append(" WHERE u.PublicId = @id AND u.IsDeleted = 0 ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@id", _id) }, "F:AdminUser:GetRow", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            return _row == null ? null : MapAdminUser(_row);
        }

        /// <summary>The acting admin's PublicId and Super Admin flag (empty when unknown).</summary>
        private async Task<(Guid PublicId, bool IsSuperAdmin)> GetActorAsync(long? _actorUserId, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT PublicId, IsSuperAdmin FROM dbo.AdminUsers ");
            _sqlQuery.Append(" WHERE Id = @id AND IsDeleted = 0 ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@id", _actorUserId ?? 0) }, "F:AdminUser:GetActor", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            return _row == null ? (Guid.Empty, false) : (_row.GetGuid("PublicId"), _row.GetBool("IsSuperAdmin"));
        }

        private static IFunctionReturn Forbidden(string _message, string _methodName)
            => FunctionReturn.Fail(Constants.ReturnMessageTypes.PermissionDenied, _message, Constants.HttpStatusCodes.Forbidden, _methodName);

        /// <summary>Loads the target and enforces "only a Super Admin can change a Super Admin". Returns the failure, or null when allowed.</summary>
        private async Task<(AdminUserDto target, IFunctionReturn failure)> LoadTargetAsync(Guid _id, long? _actorUserId, string _methodName, CancellationToken _ct)
        {
            AdminUserDto _target = await GetRowAsync(_id, _ct);
            if (_target == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.AdminUserNotFound, _methodName));
            if (_target.IsSuperAdmin && !(await GetActorAsync(_actorUserId, _ct)).IsSuperAdmin)
                return (null, Forbidden(ApplicationConstants.ValidationMessages.SuperAdminOnly, _methodName));
            return (_target, null);
        }

        /// <summary>Audit snapshot: profile and group only, never password data.</summary>
        private static object Snapshot(AdminUserDto u) => u == null ? null : new { u.FirstName, u.LastName, u.Username, u.Email, u.MobileNumber, u.GroupId, u.GroupName, u.Status };

        private Task AuditAsync(long? _actorUserId, ApplicationEnumeration.AuditAction _action, Guid _id, object _old, object _new, string _ipAddress, CancellationToken _ct)
            => _auditFunctions.LogAsync(_actorUserId, _action.ToString(), ApplicationEnumeration.ObjectType.AdminUser.ToString(), _id.ToString(), _old, _new, _ipAddress, _ct);

        private static FunctionReturn Ok(string _methodName, string _message, string _httpStatus = Constants.HttpStatusCodes.Ok)
            => new FunctionReturn { MethodName = _methodName, HttpStatusCode = _httpStatus, Message = new List<string> { _message } };

        #endregion

        public async Task<(PagedResult<AdminUserDto> result, IFunctionReturn functionReturn)> GetAdminUsersAsync(AdminUserFilter _filter, CancellationToken _ct = default)
        {
            string _methodName = "F:AdminUser:GetAdminUsers";
            try
            {
                _filter = _filter ?? new AdminUserFilter();
                PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortKeys, "name");
                if (string.IsNullOrWhiteSpace(_filter.SortDirection)) _paging.SortDirection = "ASC";

                string _status = _filter.Status == ApplicationDatabaseConstants.Status.Active || _filter.Status == ApplicationDatabaseConstants.Status.Inactive
                    || _filter.Status == ApplicationDatabaseConstants.Status.Locked ? _filter.Status : null;
                string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
                var _totalCount = IntOut("@TotalCount");
                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.AdminUserGetPaged, new[]
                {
                    P("@Search", _search), P("@Status", _status), P("@GroupPublicId", _filter.GroupId),
                    P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection), P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                return (new PagedResult<AdminUserDto>
                {
                    Items = _dataTable.MapRows(MapAdminUser),
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

        public async Task<(AdminUserDto user, IFunctionReturn functionReturn)> GetAdminUserAsync(Guid _id, CancellationToken _ct = default)
        {
            string _methodName = "F:AdminUser:GetAdminUser";
            try
            {
                AdminUserDto _user = await GetRowAsync(_id, _ct);
                if (_user == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.AdminUserNotFound, _methodName));
                return (_user, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(List<GroupOption> groups, IFunctionReturn functionReturn)> GetGroupOptionsAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:AdminUser:GetGroupOptions";
            try
            {
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT PublicId, Name, Code FROM dbo.PermissionGroups ");
                _sqlQuery.Append(" WHERE Status = 'Active' ORDER BY Name ");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), null, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                return (_dataTable.MapRows(_row => new GroupOption { Id = _row.GetGuid("PublicId"), Name = _row.GetText("Name"), Code = _row.GetText("Code") }), _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(AdminUserDto user, IFunctionReturn functionReturn)> CreateAdminUserAsync(HttpAdminUserPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:AdminUser:CreateAdminUser";
            IFunctionReturn _functionReturn;
            Guid _id;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateAdminUser(_params?.FirstName, _params?.LastName, _params?.Username?.Trim(), true, _params?.Email?.Trim(),
                    _params?.MobileNumber?.Trim(), _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);
                _functionReturn = ApplicationValidationFunctions.ValidateNewPassword(_params.Password, _params.ConfirmPassword, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                // The procedure checks duplicates and the group (must be active) under one lock.
                try
                {
                    _id = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.AdminUserCreate, new[]
                    {
                        P("@FirstName", Clean(_params.FirstName)), P("@LastName", Clean(_params.LastName)), P("@Username", _params.Username.Trim()),
                        P("@Email", _params.Email.Trim()), P("@MobileNumber", Clean(_params.MobileNumber)),
                        P("@PasswordHash", SecurityHash.HashPassword(_params.Password, _authSettings.PasswordHashIterations)),
                        P("@GroupPublicId", _params.GroupId), P("@ActorUserId", _actorUserId)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                AdminUserDto _created = await GetRowAsync(_id, _ct);
                await AuditAsync(_actorUserId, ApplicationEnumeration.AuditAction.Create, _id, null, Snapshot(_created), _ipAddress, _ct);
                return (_created, Ok(_methodName, "Admin user created.", Constants.HttpStatusCodes.Created));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(AdminUserDto user, IFunctionReturn functionReturn)> UpdateAdminUserAsync(Guid _id, HttpAdminUserPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:AdminUser:UpdateAdminUser";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateAdminUser(_params?.FirstName, _params?.LastName, null, false, _params?.Email?.Trim(),
                    _params?.MobileNumber?.Trim(), _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                (AdminUserDto _existing, IFunctionReturn _failure) = await LoadTargetAsync(_id, _actorUserId, _methodName, _ct);
                if (_failure != null) return (null, _failure);

                // A group change replaces the user's group and bumps their permission version (new access on the next request).
                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.AdminUserUpdate, new[]
                {
                    P("@PublicId", _id), P("@FirstName", Clean(_params.FirstName)), P("@LastName", Clean(_params.LastName)), P("@Email", _params.Email.Trim()),
                    P("@MobileNumber", Clean(_params.MobileNumber)), P("@GroupPublicId", _params.GroupId), P("@ActorUserId", _actorUserId)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                AdminUserDto _updated = await GetRowAsync(_id, _ct);
                await AuditAsync(_actorUserId, _existing.GroupId != _updated.GroupId ? ApplicationEnumeration.AuditAction.AssignGroup : ApplicationEnumeration.AuditAction.Update,
                    _id, Snapshot(_existing), Snapshot(_updated), _ipAddress, _ct);
                return (_updated, Ok(_methodName, "Admin user updated."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(AdminUserDto user, IFunctionReturn functionReturn)> SetStatusAsync(Guid _id, HttpUserStatusParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:AdminUser:SetStatus";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                string _status = _params?.Status;
                if (_status != ApplicationDatabaseConstants.Status.Active && _status != ApplicationDatabaseConstants.Status.Inactive)
                    return (null, ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.SalesStatusInvalid, _methodName));

                (AdminUserDto _existing, IFunctionReturn _failure) = await LoadTargetAsync(_id, _actorUserId, _methodName, _ct);
                if (_failure != null) return (null, _failure);
                if (_status == ApplicationDatabaseConstants.Status.Inactive && (await GetActorAsync(_actorUserId, _ct)).PublicId == _id)
                    return (null, ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.CannotDeactivateSelf, _methodName));

                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.AdminUserSetStatus,
                    new[] { P("@PublicId", _id), P("@Status", _status), P("@ActorUserId", _actorUserId) }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                AdminUserDto _updated = await GetRowAsync(_id, _ct);
                await AuditAsync(_actorUserId, ApplicationEnumeration.AuditAction.StatusChange, _id,
                    new { _existing.Status, _existing.IsLockedOut }, new { _updated.Status, _updated.IsLockedOut }, _ipAddress, _ct);
                return (_updated, Ok(_methodName, _status == ApplicationDatabaseConstants.Status.Active ? "Admin user activated." : "Admin user deactivated."));
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
            string _methodName = "F:AdminUser:ResetPassword";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateNewPassword(_params?.NewPassword, _params?.ConfirmPassword, _methodName);
                if (!_functionReturn.Status) return _functionReturn;

                (AdminUserDto _, IFunctionReturn _failure) = await LoadTargetAsync(_id, _actorUserId, _methodName, _ct);
                if (_failure != null) return _failure;
                if ((await GetActorAsync(_actorUserId, _ct)).PublicId == _id)
                    return ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.UseChangePasswordForSelf, _methodName);

                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.AdminUserAdminSetPassword, new[]
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

        public async Task<(ProfileDto profile, IFunctionReturn functionReturn)> GetProfileAsync(Guid _id, CancellationToken _ct = default)
        {
            string _methodName = "F:AdminUser:GetProfile";
            try
            {
                AdminUserDto _user = await GetRowAsync(_id, _ct);
                if (_user == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.AdminUserNotFound, _methodName));

                DateTime _passwordUpdated = DateTime.SpecifyKind(await _databaseFunctions.ExecuteScalarAsync<DateTime>(
                    " SELECT PasswordUpdatedAtUtc FROM dbo.AdminUsers WHERE PublicId = @id ", new[] { P("@id", _id) }, _ct), DateTimeKind.Utc);

                // The last 10 changes this admin made (IX_AuditLogs_ActorUserId: ActorUserId, CreatedAtUtc DESC).
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT TOP (10) a.Id, a.Action, a.EntityType, a.EntityId, a.CreatedAtUtc ");
                _sqlQuery.Append(" FROM dbo.AuditLogs a ");
                _sqlQuery.Append(" JOIN dbo.AdminUsers u ON u.Id = a.ActorUserId ");
                _sqlQuery.Append(" WHERE u.PublicId = @id ");
                _sqlQuery.Append(" ORDER BY a.CreatedAtUtc DESC, a.Id DESC ");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                    _sqlQuery.ToString(), new[] { P("@id", _id) }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                List<ProfileActivity> _activity = _dataTable.MapRows(_row => new ProfileActivity
                {
                    Id = _row.GetLong("Id"),
                    Action = _row.GetText("Action"),
                    EntityType = _row.GetText("EntityType"),
                    EntityId = _row.GetText("EntityId"),
                    CreatedAtUtc = _row.GetUtc("CreatedAtUtc")
                });

                return (new ProfileDto
                {
                    Id = _user.Id, FirstName = _user.FirstName, LastName = _user.LastName, Username = _user.Username, Email = _user.Email,
                    MobileNumber = _user.MobileNumber, Status = _user.Status, IsSuperAdmin = _user.IsSuperAdmin, GroupId = _user.GroupId,
                    GroupName = _user.GroupName, GroupIsActive = _user.GroupIsActive, MustChangePassword = _user.MustChangePassword,
                    IsLockedOut = _user.IsLockedOut, LastLoginAtUtc = _user.LastLoginAtUtc, CreatedAtUtc = _user.CreatedAtUtc,
                    PasswordUpdatedAtUtc = _passwordUpdated, RecentActivity = _activity
                }, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        /// <summary>The signed-in admin changes their own name and mobile number (email, group and status stay as they are).</summary>
        public async Task<(ProfileDto profile, IFunctionReturn functionReturn)> UpdateProfileAsync(Guid _id, HttpProfilePutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:AdminUser:UpdateProfile";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                AdminUserDto _existing = await GetRowAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.AdminUserNotFound, _methodName));

                _functionReturn = ApplicationValidationFunctions.ValidateAdminUser(_params?.FirstName, _params?.LastName, null, false, _existing.Email,
                    _params?.MobileNumber?.Trim(), _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.AdminUserUpdateProfile, new[]
                {
                    P("@PublicId", _id), P("@FirstName", Clean(_params.FirstName)), P("@LastName", Clean(_params.LastName)), P("@MobileNumber", Clean(_params.MobileNumber))
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                AdminUserDto _updated = await GetRowAsync(_id, _ct);
                await AuditAsync(_actorUserId, ApplicationEnumeration.AuditAction.UpdateProfile, _id, Snapshot(_existing), Snapshot(_updated), _ipAddress, _ct);
                (ProfileDto _profile, IFunctionReturn _read) = await GetProfileAsync(_id, _ct);
                if (!_read.Status) return (null, _read);
                return (_profile, Ok(_methodName, "Your profile has been saved."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
