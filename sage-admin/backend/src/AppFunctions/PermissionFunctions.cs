using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
    public class PermissionFunctions : IPermissionFunctions
    {
        /// <summary>
        /// Sort keys the client may request. dbo.usp_Permission_GetPaged maps each key to a fixed ORDER BY; anything else falls back to "module".
        /// </summary>
        private static readonly string[] SortKeys = { "module", "code", "name", "action", "isActive", "createdAtUtc" };

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly IAuditFunctions _auditFunctions;

        public PermissionFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IAuditFunctions __auditFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _auditFunctions = __auditFunctions;
        }

        #region Helpers

        private static ModuleDto MapModule(DataRow _row) => new ModuleDto
        {
            Id = _row.GetLong("Id"),
            Code = _row.GetText("Code"),
            Name = _row.GetText("Name"),
            Category = _row.GetText("Category"),
            SortOrder = _row.GetInt("SortOrder"),
            IsActive = _row.GetBool("IsActive"),
            IsSystem = _row.GetBool("IsSystem"),
            PermissionCount = _row.GetInt("PermissionCount"),
            MenuPath = _row.GetText("MenuPath")
        };

        /// <summary>One permission row (from the queries below or dbo.usp_Permission_GetPaged; both use these column names).</summary>
        private static PermissionDto MapPermission(DataRow _row) => new PermissionDto
        {
            Id = _row.GetLong("Id"),
            ModuleId = _row.GetLong("ModuleId"),
            ModuleCode = _row.GetText("ModuleCode"),
            ModuleName = _row.GetText("ModuleName"),
            Action = _row.GetText("Action"),
            Code = _row.GetText("Code"),
            Name = _row.GetText("Name"),
            Description = _row.GetText("Description"),
            IsActive = _row.GetBool("IsActive"),
            IsSystem = _row.GetBool("IsSystem")
        };

        /// <summary>SELECT ... FROM for modules (with their permission count); the caller adds WHERE / ORDER BY.</summary>
        private static StringBuilder ModuleQuery()
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT m.Id, m.Code, m.Name, m.Category, m.SortOrder, m.IsActive, m.IsSystem, ");
            _sqlQuery.Append("        (SELECT COUNT(*) FROM dbo.Permissions px WHERE px.ModuleId = m.Id) AS PermissionCount, m.MenuPath ");
            _sqlQuery.Append(" FROM dbo.Modules m ");
            return _sqlQuery;
        }

        /// <summary>SELECT ... FROM for permissions (with their module); the caller adds WHERE / ORDER BY.</summary>
        private static StringBuilder PermissionQuery()
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT p.Id, p.ModuleId, m.Code AS ModuleCode, m.Name AS ModuleName, p.Action, p.Code, p.Name, p.Description, p.IsActive, p.IsSystem ");
            _sqlQuery.Append(" FROM dbo.Permissions p ");
            _sqlQuery.Append(" JOIN dbo.Modules m ON m.Id = p.ModuleId ");
            return _sqlQuery;
        }

        /// <summary>A 409 from a create procedure carries a generic message; replace it with the screen's own wording.</summary>
        private static IFunctionReturn WithConflictMessage(IFunctionReturn _functionReturn, string _message)
        {
            if (_functionReturn.HttpStatusCode == Constants.HttpStatusCodes.Conflict) _functionReturn.Message = new List<string> { _message };
            return _functionReturn;
        }

        private async Task<ModuleDto> GetModuleByIdAsync(long _id, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = ModuleQuery();
            _sqlQuery.Append(" WHERE m.Id = @id ");
            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@id", _id) }, "F:Permission:GetModuleById", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            return _row == null ? null : MapModule(_row);
        }

        private async Task<PermissionDto> GetPermissionByIdAsync(long _id, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = PermissionQuery();
            _sqlQuery.Append(" WHERE p.Id = @id ");
            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@id", _id) }, "F:Permission:GetPermissionById", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            return _row == null ? null : MapPermission(_row);
        }

        /// <summary>Audit snapshot: only business fields, never secrets.</summary>
        private static object Snapshot(ModuleDto m) => m == null ? null : new { m.Code, m.Name, m.Category, m.SortOrder, m.IsActive, m.MenuPath };
        private static object Snapshot(PermissionDto p) => p == null ? null : new { p.Code, p.Name, p.Description, p.IsActive };

        #endregion

        public async Task<(PagedResult<PermissionDto> result, IFunctionReturn functionReturn)> GetPermissionsAsync(PermissionFilter _filter, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Permission:GetPermissions";
            #endregion
            try
            {
                _filter = _filter ?? new PermissionFilter();
                PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortKeys, "module");
                // Default direction for browsing the catalogue is ascending.
                if (string.IsNullOrWhiteSpace(_filter.SortDirection)) _paging.SortDirection = "ASC";

                string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
                var _totalCount = IntOut("@TotalCount");
                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.PermissionGetPaged, new[]
                {
                    P("@ModuleId", _filter.ModuleId), P("@IsActive", _filter.IsActive), P("@Search", _search),
                    P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection),
                    P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                return (new PagedResult<PermissionDto>
                {
                    Items = _dataTable.MapRows(MapPermission),
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

        public async Task<(List<ModuleDto> modules, IFunctionReturn functionReturn)> GetModulesAsync(bool _includeInactive, CancellationToken _ct = default)
        {
            string _methodName = "F:Permission:GetModules";
            try
            {
                StringBuilder _moduleQuery = ModuleQuery();
                _moduleQuery.Append(" WHERE (@inc = 1 OR m.IsActive = 1) ");
                _moduleQuery.Append(" ORDER BY m.SortOrder, m.Name ");
                (IFunctionReturn _functionReturn, DataTable _moduleTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                    _moduleQuery.ToString(), new[] { P("@inc", _includeInactive) }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                StringBuilder _permissionQuery = PermissionQuery();
                _permissionQuery.Append(" WHERE (@inc = 1 OR (p.IsActive = 1 AND m.IsActive = 1)) ");
                _permissionQuery.Append(" ORDER BY p.Code ");
                (_functionReturn, DataTable _permissionTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                    _permissionQuery.ToString(), new[] { P("@inc", _includeInactive) }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                List<ModuleDto> _modules = _moduleTable.MapRows(MapModule);
                ILookup<long, PermissionDto> _byModule = _permissionTable.MapRows(MapPermission).ToLookup(p => p.ModuleId);
                foreach (ModuleDto _module in _modules) _module.Permissions = _byModule[_module.Id].ToList();
                return (_modules, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(ModuleDto module, IFunctionReturn functionReturn)> CreateModuleAsync(HttpModulePostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Permission:CreateModule";
            IFunctionReturn _functionReturn;
            long _id;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateModule(_params?.Code, _params?.Name, _params?.Category, _params?.MenuPath, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                // The procedure checks and inserts under one lock; a duplicate code comes back as a 409.
                (_functionReturn, _id) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.ModuleCreate, new[]
                {
                    P("@Code", _params.Code.Trim()), P("@Name", Clean(_params.Name)), P("@Category", Clean(_params.Category)), P("@SortOrder", _params.SortOrder),
                    P("@MenuPath", string.IsNullOrWhiteSpace(_params.MenuPath) ? null : _params.MenuPath.Trim())
                }, _methodName, "Module created.", _ct);
                if (!_functionReturn.Status) return (null, WithConflictMessage(_functionReturn, ApplicationConstants.ValidationMessages.ModuleCodeExists));

                ModuleDto _created = await GetModuleByIdAsync(_id, _ct);
                await _auditFunctions.LogAsync(_actorUserId, ApplicationEnumeration.AuditAction.Create.ToString(), ApplicationEnumeration.ObjectType.Module.ToString(), _id.ToString(), null, Snapshot(_created), _ipAddress, _ct);
                _functionReturn.HttpStatusCode = Constants.HttpStatusCodes.Created;
                return (_created, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(ModuleDto module, IFunctionReturn functionReturn)> UpdateModuleAsync(long _id, HttpModulePutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Permission:UpdateModule";
            IFunctionReturn _functionReturn;
            long _retValue;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateModuleUpdate(_params?.Name, _params?.Category, _params?.MenuPath, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ModuleDto _existing = await GetModuleByIdAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.ModuleNotFound, _methodName));
                if (_existing.IsSystem && !_params.IsActive) return (null, ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.SystemModuleDeactivate, _methodName));

                bool _activeChanged = _existing.IsActive != _params.IsActive;

                // When the active flag changes, the procedure bumps the permission version of every user who holds one of the
                // module's permissions, so their tokens are re-evaluated on the next request.
                (_functionReturn, _retValue) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.ModuleUpdate, new[]
                {
                    P("@Id", _id), P("@Name", Clean(_params.Name)), P("@Category", Clean(_params.Category)), P("@SortOrder", _params.SortOrder), P("@IsActive", _params.IsActive),
                    // Not sent = keep the current link (older clients do not know the field); empty = remove it.
                    P("@MenuPath", _params.MenuPath == null ? _existing.MenuPath : (string.IsNullOrWhiteSpace(_params.MenuPath) ? null : _params.MenuPath.Trim()))
                }, _methodName, "Module updated.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                if (_retValue == 0) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.ModuleNotFound, _methodName));

                ModuleDto _updated = await GetModuleByIdAsync(_id, _ct);
                await _auditFunctions.LogAsync(_actorUserId, (_activeChanged ? ApplicationEnumeration.AuditAction.StatusChange : ApplicationEnumeration.AuditAction.Update).ToString(),
                    ApplicationEnumeration.ObjectType.Module.ToString(), _id.ToString(), Snapshot(_existing), Snapshot(_updated), _ipAddress, _ct);
                return (_updated, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(PermissionDto permission, IFunctionReturn functionReturn)> CreatePermissionAsync(HttpPermissionPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Permission:CreatePermission";
            IFunctionReturn _functionReturn;
            long _id;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidatePermission(_params?.Action, _params?.Name, _params?.Description, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ModuleDto _module = await GetModuleByIdAsync(_params.ModuleId, _ct);
                if (_module == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.ModuleNotFound, _methodName));
                if (!_module.IsActive) return (null, ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.ModuleInactive, _methodName));

                // The procedure builds Code = Module.Action, then checks and inserts under one lock; a duplicate comes back as a 409.
                (_functionReturn, _id) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.PermissionCreate, new[]
                {
                    P("@ModuleId", _module.Id), P("@Action", _params.Action.Trim()), P("@Name", Clean(_params.Name)), P("@Description", Clean(_params.Description))
                }, _methodName, "Permission created.", _ct);
                if (!_functionReturn.Status)
                {
                    if (_functionReturn.HttpStatusCode == Constants.HttpStatusCodes.NotFound)
                        return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.ModuleNotFound, _methodName));
                    return (null, WithConflictMessage(_functionReturn, ApplicationConstants.ValidationMessages.PermissionCodeExists));
                }

                PermissionDto _created = await GetPermissionByIdAsync(_id, _ct);
                await _auditFunctions.LogAsync(_actorUserId, ApplicationEnumeration.AuditAction.Create.ToString(), ApplicationEnumeration.ObjectType.Permission.ToString(), _id.ToString(), null, Snapshot(_created), _ipAddress, _ct);
                _functionReturn.HttpStatusCode = Constants.HttpStatusCodes.Created;
                return (_created, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(PermissionDto permission, IFunctionReturn functionReturn)> UpdatePermissionAsync(long _id, HttpPermissionPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Permission:UpdatePermission";
            IFunctionReturn _functionReturn;
            long _retValue;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidatePermissionUpdate(_params?.Name, _params?.Description, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                PermissionDto _existing = await GetPermissionByIdAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.PermissionNotFound, _methodName));
                if (_existing.IsSystem && !_params.IsActive) return (null, ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.SystemPermissionDeactivate, _methodName));

                bool _activeChanged = _existing.IsActive != _params.IsActive;

                // The procedure also bumps the permission version of users holding it when the active flag changes.
                (_functionReturn, _retValue) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.PermissionUpdate, new[]
                {
                    P("@Id", _id), P("@Name", Clean(_params.Name)), P("@Description", Clean(_params.Description)), P("@IsActive", _params.IsActive)
                }, _methodName, "Permission updated.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                if (_retValue == 0) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.PermissionNotFound, _methodName));

                PermissionDto _updated = await GetPermissionByIdAsync(_id, _ct);
                await _auditFunctions.LogAsync(_actorUserId, (_activeChanged ? ApplicationEnumeration.AuditAction.StatusChange : ApplicationEnumeration.AuditAction.Update).ToString(),
                    ApplicationEnumeration.ObjectType.Permission.ToString(), _id.ToString(), Snapshot(_existing), Snapshot(_updated), _ipAddress, _ct);
                return (_updated, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
