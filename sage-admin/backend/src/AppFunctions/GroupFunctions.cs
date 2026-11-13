using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
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

        private const string GroupSelect = @"SELECT g.PublicId, g.Code, g.Name, g.Description, g.Status, g.IsSystem,
    (SELECT COUNT(*) FROM dbo.AdminUserGroups ug JOIN dbo.AdminUsers au ON au.Id = ug.UserId AND au.IsDeleted = 0 WHERE ug.GroupId = g.Id),
    (SELECT COUNT(*) FROM dbo.GroupPermissions gp WHERE gp.GroupId = g.Id),
    g.CreatedAtUtc, g.UpdatedAtUtc
FROM dbo.PermissionGroups g WHERE g.PublicId = @id";

        /// <summary>Every active permission in an active module, plus anything the group still holds that has since been deactivated.</summary>
        private const string MatrixSelect = @"SELECT m.Id, m.Code, m.Name, m.Category, m.IsActive,
    p.Id, p.Code, p.Action, p.Name, p.Description, CAST(CASE WHEN p.IsActive = 1 AND m.IsActive = 1 THEN 1 ELSE 0 END AS BIT),
    CAST(CASE WHEN gp.PermissionId IS NULL THEN 0 ELSE 1 END AS BIT)
FROM dbo.Permissions p
JOIN dbo.Modules m ON m.Id = p.ModuleId
LEFT JOIN dbo.GroupPermissions gp ON gp.PermissionId = p.Id AND gp.GroupId = (SELECT Id FROM dbo.PermissionGroups WHERE PublicId = @id)
WHERE (p.IsActive = 1 AND m.IsActive = 1) OR gp.PermissionId IS NOT NULL
ORDER BY m.SortOrder, m.Name, p.Id";

        private const string AssignedCodesSelect = @"SELECT p.Code FROM dbo.GroupPermissions gp
JOIN dbo.Permissions p ON p.Id = gp.PermissionId
JOIN dbo.PermissionGroups g ON g.Id = gp.GroupId
WHERE g.PublicId = @id ORDER BY p.Code";

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

        private static DateTime UtcValue(SqlDataReader r, int i) => DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);

        private static GroupDto MapGroup(SqlDataReader r) => new GroupDto
        {
            Id = r.GetGuid(0),
            Code = r.GetString(1),
            Name = r.GetString(2),
            Description = r.IsDBNull(3) ? null : r.GetString(3),
            Status = r.GetString(4),
            IsSystem = r.GetBoolean(5),
            UserCount = r.GetInt32(6),
            PermissionCount = r.GetInt32(7),
            CreatedAtUtc = UtcValue(r, 8),
            UpdatedAtUtc = r.IsDBNull(9) ? (DateTime?)null : UtcValue(r, 9)
        };

        private Task<GroupDto> GetGroupRowAsync(Guid _id, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync(GroupSelect, MapGroup, new[] { P("@id", _id) }, _ct);

        private async Task<List<string>> GetAssignedCodesAsync(Guid _id, CancellationToken _ct)
            => (await _databaseFunctions.QueryAsync(AssignedCodesSelect, r => r.GetString(0), new[] { P("@id", _id) }, _ct)).ToList();

        private async Task<GroupDetailDto> LoadDetailAsync(Guid _id, CancellationToken _ct)
        {
            GroupDto _group = await GetGroupRowAsync(_id, _ct);
            if (_group == null) return null;

            var _rows = await _databaseFunctions.QueryAsync(MatrixSelect, r => (
                Module: new GroupMatrixModule
                {
                    Id = r.GetInt64(0), Code = r.GetString(1), Name = r.GetString(2), Category = r.IsDBNull(3) ? null : r.GetString(3), IsActive = r.GetBoolean(4)
                },
                Permission: new GroupMatrixPermission
                {
                    Id = r.GetInt64(5), Code = r.GetString(6), Action = r.GetString(7), Name = r.GetString(8),
                    Description = r.IsDBNull(9) ? null : r.GetString(9), IsActive = r.GetBoolean(10), Assigned = r.GetBoolean(11)
                }), new[] { P("@id", _id) }, _ct);

            var _modules = new List<GroupMatrixModule>();
            foreach (var _row in _rows)
            {
                GroupMatrixModule _module = _modules.Count > 0 && _modules[^1].Id == _row.Module.Id ? _modules[^1] : null;
                if (_module == null) { _module = _row.Module; _modules.Add(_module); }
                _module.Permissions.Add(_row.Permission);
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
                var _parameters = new List<SqlParameter>
                {
                    P("@Search", _search), P("@Status", _status), P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection),
                    P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                };
                IReadOnlyList<GroupDto> _items = await _databaseFunctions.QueryProcedureAsync(SP.GroupGetPaged, MapGroup, _parameters, _ct);
                int _total = OutInt(_totalCount);

                return (new PagedResult<GroupDto> { Items = _items, Page = _paging.Page, PageSize = _paging.PageSize, TotalCount = _total }, new FunctionReturn { MethodName = _methodName });
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
