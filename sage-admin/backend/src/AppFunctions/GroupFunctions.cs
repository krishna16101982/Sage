using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
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
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Permission groups: list, view (with permission matrix), create, edit, delete, and replace the permission set.
    /// Writes go through stored procedures (atomic checks, permission-version bumps for members).
    /// </summary>
    public class GroupFunctions : IGroupFunctions
    {
        /// <summary>Sort keys the client may request. dbo.usp_Group_GetPaged maps each to a fixed ORDER BY.</summary>
        private static readonly string[] SortKeys = { "name", "code", "status", "userCount", "createdAtUtc" };

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly IAuditFunctions _auditFunctions;

        public GroupFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IAuditFunctions __auditFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _auditFunctions = __auditFunctions;
        }

        #region Helpers

        /// <summary>One group row (from GetGroupRowAsync or dbo.usp_Group_GetPaged; both use these column names).</summary>
        private static GroupDto MapGroup(DataRow _row) => new GroupDto
        {
            Id = _row.GetGuid("PublicId"),
            Code = _row.GetText("Code"),
            Name = _row.GetText("Name"),
            Description = _row.GetText("Description"),
            Status = _row.GetText("Status"),
            IsSystem = _row.GetBool("IsSystem"),
            UserCount = _row.GetInt("UserCount"),
            PermissionCount = _row.GetInt("PermissionCount"),
            CreatedAtUtc = _row.GetUtc("CreatedAtUtc"),
            UpdatedAtUtc = _row.GetNullableUtc("UpdatedAtUtc")
        };

        /// <summary>One group by PublicId with its member and permission counts, or null.</summary>
        private async Task<GroupDto> GetGroupRowAsync(Guid _id, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT g.PublicId, g.Code, g.Name, g.Description, g.Status, g.IsSystem, ");
            _sqlQuery.Append("        (SELECT COUNT(*) FROM dbo.AdminUserGroups ug JOIN dbo.AdminUsers au ON au.Id = ug.UserId AND au.IsDeleted = 0 ");
            _sqlQuery.Append("         WHERE ug.GroupId = g.Id) AS UserCount, ");
            _sqlQuery.Append("        (SELECT COUNT(*) FROM dbo.GroupPermissions gp WHERE gp.GroupId = g.Id) AS PermissionCount, ");
            _sqlQuery.Append("        g.CreatedAtUtc, g.UpdatedAtUtc ");
            _sqlQuery.Append(" FROM dbo.PermissionGroups g ");
            _sqlQuery.Append(" WHERE g.PublicId = @id ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@id", _id) }, "F:Group:GetGroupRow", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            return _row == null ? null : MapGroup(_row);
        }

        /// <summary>The permission codes the group holds (for the audit trail).</summary>
        private async Task<List<string>> GetAssignedCodesAsync(Guid _id, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT p.Code FROM dbo.GroupPermissions gp ");
            _sqlQuery.Append(" JOIN dbo.Permissions p ON p.Id = gp.PermissionId ");
            _sqlQuery.Append(" JOIN dbo.PermissionGroups g ON g.Id = gp.GroupId ");
            _sqlQuery.Append(" WHERE g.PublicId = @id ORDER BY p.Code ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@id", _id) }, "F:Group:GetAssignedCodes", _ct);
            EnsureRead(_functionReturn);
            return _dataTable.MapRows(_row => _row.GetText("Code"));
        }

        /// <summary>
        /// The group with its permission matrix: every active permission in an active module, plus anything the group still
        /// holds that has since been deactivated, grouped by module.
        /// </summary>
        private async Task<GroupDetailDto> LoadDetailAsync(Guid _id, CancellationToken _ct)
        {
            GroupDto _group = await GetGroupRowAsync(_id, _ct);
            if (_group == null) return null;

            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT m.Id AS ModuleId, m.Code AS ModuleCode, m.Name AS ModuleName, m.Category, m.IsActive AS ModuleIsActive, ");
            _sqlQuery.Append("        p.Id AS PermissionId, p.Code AS PermissionCode, p.Action, p.Name AS PermissionName, p.Description, ");
            _sqlQuery.Append("        CAST(CASE WHEN p.IsActive = 1 AND m.IsActive = 1 THEN 1 ELSE 0 END AS BIT) AS PermissionIsActive, ");
            _sqlQuery.Append("        CAST(CASE WHEN gp.PermissionId IS NULL THEN 0 ELSE 1 END AS BIT) AS Assigned ");
            _sqlQuery.Append(" FROM dbo.Permissions p ");
            _sqlQuery.Append(" JOIN dbo.Modules m ON m.Id = p.ModuleId ");
            _sqlQuery.Append(" LEFT JOIN dbo.GroupPermissions gp ON gp.PermissionId = p.Id ");
            _sqlQuery.Append("      AND gp.GroupId = (SELECT Id FROM dbo.PermissionGroups WHERE PublicId = @id) ");
            _sqlQuery.Append(" WHERE (p.IsActive = 1 AND m.IsActive = 1) OR gp.PermissionId IS NOT NULL ");
            _sqlQuery.Append(" ORDER BY m.SortOrder, m.Name, p.Id ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@id", _id) }, "F:Group:LoadDetail", _ct);
            EnsureRead(_functionReturn);

            var _modules = new List<GroupMatrixModule>();
            foreach (DataRow _row in _dataTable.Rows)
            {
                long _moduleId = _row.GetLong("ModuleId");
                GroupMatrixModule _module = _modules.Count > 0 && _modules[^1].Id == _moduleId ? _modules[^1] : null;
                if (_module == null)
                {
                    _module = new GroupMatrixModule
                    {
                        Id = _moduleId,
                        Code = _row.GetText("ModuleCode"),
                        Name = _row.GetText("ModuleName"),
                        Category = _row.GetText("Category"),
                        IsActive = _row.GetBool("ModuleIsActive")
                    };
                    _modules.Add(_module);
                }
                _module.Permissions.Add(new GroupMatrixPermission
                {
                    Id = _row.GetLong("PermissionId"),
                    Code = _row.GetText("PermissionCode"),
                    Action = _row.GetText("Action"),
                    Name = _row.GetText("PermissionName"),
                    Description = _row.GetText("Description"),
                    IsActive = _row.GetBool("PermissionIsActive"),
                    Assigned = _row.GetBool("Assigned")
                });
            }

            return new GroupDetailDto
            {
                Id = _group.Id, Code = _group.Code, Name = _group.Name, Description = _group.Description, Status = _group.Status,
                IsSystem = _group.IsSystem, UserCount = _group.UserCount, PermissionCount = _group.PermissionCount,
                CreatedAtUtc = _group.CreatedAtUtc, UpdatedAtUtc = _group.UpdatedAtUtc, Modules = _modules
            };
        }

        /// <summary>Audit snapshot: business fields only.</summary>
        private static object Snapshot(GroupDto g) => g == null ? null : new { g.Code, g.Name, g.Description, g.Status };

        #endregion

        public async Task<(PagedResult<GroupDto> result, IFunctionReturn functionReturn)> GetGroupsAsync(GroupFilter _filter, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Group:GetGroups";
            #endregion
            try
            {
                _filter = _filter ?? new GroupFilter();
                PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortKeys, "name");
                if (string.IsNullOrWhiteSpace(_filter.SortDirection)) _paging.SortDirection = "ASC";

                string _status = _filter.Status == ApplicationDatabaseConstants.Status.Active || _filter.Status == ApplicationDatabaseConstants.Status.Inactive ? _filter.Status : null;
                string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
                var _totalCount = IntOut("@TotalCount");
                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.GroupGetPaged, new[]
                {
                    P("@Search", _search), P("@Status", _status), P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection),
                    P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                return (new PagedResult<GroupDto>
                {
                    Items = _dataTable.MapRows(MapGroup),
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

        public async Task<(GroupDetailDto group, IFunctionReturn functionReturn)> GetGroupAsync(Guid _id, CancellationToken _ct = default)
        {
            string _methodName = "F:Group:GetGroup";
            try
            {
                GroupDetailDto _group = await LoadDetailAsync(_id, _ct);
                if (_group == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.GroupNotFound, _methodName));
                return (_group, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(GroupDto group, IFunctionReturn functionReturn)> CreateGroupAsync(HttpGroupPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Group:CreateGroup";
            IFunctionReturn _functionReturn;
            Guid _id;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateGroup(_params?.Name, _params?.Code, _params?.Description, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                // The procedure checks code and name and inserts under one lock; a duplicate comes back as a 409 with its message.
                try
                {
                    _id = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.GroupCreate, new[]
                    {
                        P("@Code", _params.Code.Trim()), P("@Name", Clean(_params.Name)), P("@Description", Clean(_params.Description)), P("@ActorUserId", _actorUserId)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                GroupDto _created = await GetGroupRowAsync(_id, _ct);
                await _auditFunctions.LogAsync(_actorUserId, ApplicationEnumeration.AuditAction.Create.ToString(), ApplicationEnumeration.ObjectType.PermissionGroup.ToString(), _id.ToString(), null, Snapshot(_created), _ipAddress, _ct);
                return (_created, new FunctionReturn { MethodName = _methodName, HttpStatusCode = Constants.HttpStatusCodes.Created, Message = new List<string> { "Group created." } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(GroupDto group, IFunctionReturn functionReturn)> UpdateGroupAsync(Guid _id, HttpGroupPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Group:UpdateGroup";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateGroupUpdate(_params?.Name, _params?.Description, _params?.Status, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                GroupDto _existing = await GetGroupRowAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.GroupNotFound, _methodName));
                if (_existing.IsSystem && _params.Status != ApplicationDatabaseConstants.Status.Active)
                    return (null, ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.GroupSystemDeactivate, _methodName));

                // When the status changes the procedure bumps every member's permission version.
                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.GroupUpdate, new[]
                {
                    P("@PublicId", _id), P("@Name", Clean(_params.Name)), P("@Description", Clean(_params.Description)), P("@Status", _params.Status), P("@ActorUserId", _actorUserId)
                }, _methodName, "Group updated.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                GroupDto _updated = await GetGroupRowAsync(_id, _ct);
                bool _statusChanged = _existing.Status != _updated.Status;
                await _auditFunctions.LogAsync(_actorUserId, (_statusChanged ? ApplicationEnumeration.AuditAction.StatusChange : ApplicationEnumeration.AuditAction.Update).ToString(),
                    ApplicationEnumeration.ObjectType.PermissionGroup.ToString(), _id.ToString(), Snapshot(_existing), Snapshot(_updated), _ipAddress, _ct);
                return (_updated, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<IFunctionReturn> DeleteGroupAsync(Guid _id, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Group:DeleteGroup";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                GroupDto _existing = await GetGroupRowAsync(_id, _ct);
                if (_existing == null) return ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.GroupNotFound, _methodName);
                if (_existing.IsSystem) return ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.GroupSystemDelete, _methodName);
                List<string> _codes = await GetAssignedCodesAsync(_id, _ct);

                // The procedure refuses (409) while any user is assigned to the group.
                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.GroupDelete, new[] { P("@PublicId", _id) }, _methodName, "Group deleted.", _ct);
                if (!_functionReturn.Status) return _functionReturn;

                await _auditFunctions.LogAsync(_actorUserId, ApplicationEnumeration.AuditAction.Delete.ToString(), ApplicationEnumeration.ObjectType.PermissionGroup.ToString(), _id.ToString(),
                    new { _existing.Code, _existing.Name, _existing.Description, _existing.Status, Permissions = _codes }, null, _ipAddress, _ct);
                return _functionReturn;
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemError(_methodName);
            }
        }

        public async Task<(GroupDetailDto group, IFunctionReturn functionReturn)> SetGroupPermissionsAsync(Guid _id, HttpGroupPermissionsPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Group:SetPermissions";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                if (_params?.PermissionIds == null) return (null, ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.PermissionIdsRequired, _methodName));

                List<string> _before = await GetAssignedCodesAsync(_id, _ct);

                // Replaces the whole set; the procedure validates the ids and bumps members' permission versions when anything changed.
                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.GroupSetPermissions, new[]
                {
                    P("@PublicId", _id), P("@PermissionIdsJson", JsonSerializer.Serialize(_params.PermissionIds.Distinct())), P("@ActorUserId", _actorUserId)
                }, _methodName, "Group permissions saved.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                GroupDetailDto _group = await LoadDetailAsync(_id, _ct);
                List<string> _after = _group.Modules.SelectMany(m => m.Permissions).Where(p => p.Assigned).Select(p => p.Code).OrderBy(c => c, StringComparer.Ordinal).ToList();
                if (!_before.SequenceEqual(_after))
                {
                    await _auditFunctions.LogAsync(_actorUserId, ApplicationEnumeration.AuditAction.AssignPermissions.ToString(), ApplicationEnumeration.ObjectType.PermissionGroup.ToString(), _id.ToString(),
                        new { Permissions = _before }, new { Permissions = _after, Added = _after.Except(_before).ToList(), Removed = _before.Except(_after).ToList() }, _ipAddress, _ct);
                }
                return (_group, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
