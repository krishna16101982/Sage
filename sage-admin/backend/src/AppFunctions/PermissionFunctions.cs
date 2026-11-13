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
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;

namespace SageAdminApi.AppFunctions
{
    public class PermissionFunctions : IPermissionFunctions
    {
        private const string PermissionColumns = "p.Id, p.ModuleId, m.Code, m.Name, p.Action, p.Code, p.Name, p.Description, p.IsActive, p.IsSystem";
        private const string PermissionFrom = "FROM dbo.Permissions p JOIN dbo.Modules m ON m.Id = p.ModuleId";
        private const string ModuleSelect = "SELECT m.Id, m.Code, m.Name, m.Category, m.SortOrder, m.IsActive, m.IsSystem, (SELECT COUNT(*) FROM dbo.Permissions px WHERE px.ModuleId = m.Id), m.MenuPath FROM dbo.Modules m";

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

        private static SqlParameter P(string _name, object _value) => new SqlParameter(_name, _value ?? DBNull.Value);

        private static ModuleDto MapModule(SqlDataReader r) => new ModuleDto
        {
            Id = r.GetInt64(0),
            Code = r.GetString(1),
            Name = r.GetString(2),
            Category = r.IsDBNull(3) ? null : r.GetString(3),
            SortOrder = r.GetInt32(4),
            IsActive = r.GetBoolean(5),
            IsSystem = r.GetBoolean(6),
            PermissionCount = r.GetInt32(7),
            MenuPath = r.IsDBNull(8) ? null : r.GetString(8)
        };

        private static PermissionDto MapPermission(SqlDataReader r) => new PermissionDto
        {
            Id = r.GetInt64(0),
            ModuleId = r.GetInt64(1),
            ModuleCode = r.GetString(2),
            ModuleName = r.GetString(3),
            Action = r.GetString(4),
            Code = r.GetString(5),
            Name = r.GetString(6),
            Description = r.IsDBNull(7) ? null : r.GetString(7),
            IsActive = r.GetBoolean(8),
            IsSystem = r.GetBoolean(9)
        };

        /// <summary>A 409 from a create procedure carries a generic message; replace it with the screen's own wording.</summary>
        private static IFunctionReturn WithConflictMessage(IFunctionReturn _functionReturn, string _message)
        {
            if (_functionReturn.HttpStatusCode == Constants.HttpStatusCodes.Conflict) _functionReturn.Message = new List<string> { _message };
            return _functionReturn;
        }

        private static string Clean(string _value) => string.IsNullOrWhiteSpace(_value) ? null : Sanitization.Sanitize(_value.Trim());

        private Task<ModuleDto> GetModuleByIdAsync(long _id, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync(ModuleSelect + " WHERE m.Id = @id", MapModule, new[] { P("@id", _id) }, _ct);

        private Task<PermissionDto> GetPermissionByIdAsync(long _id, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync($"SELECT {PermissionColumns} {PermissionFrom} WHERE p.Id = @id", MapPermission, new[] { P("@id", _id) }, _ct);

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
                var _totalCount = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                var _parameters = new List<SqlParameter>
                {
                    P("@ModuleId", _filter.ModuleId), P("@IsActive", _filter.IsActive), P("@Search", _search),
                    P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection),
                    P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                };
                IReadOnlyList<PermissionDto> _items = await _databaseFunctions.QueryProcedureAsync(SP.PermissionGetPaged, MapPermission, _parameters, _ct);
                int _total = _totalCount.Value is int _count ? _count : 0;

                return (new PagedResult<PermissionDto> { Items = _items, Page = _paging.Page, PageSize = _paging.PageSize, TotalCount = _total }, new FunctionReturn { MethodName = _methodName });
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
                IReadOnlyList<ModuleDto> _modules = await _databaseFunctions.QueryAsync(
                    ModuleSelect + " WHERE (@inc = 1 OR m.IsActive = 1) ORDER BY m.SortOrder, m.Name", MapModule, new[] { P("@inc", _includeInactive) }, _ct);
                IReadOnlyList<PermissionDto> _permissions = await _databaseFunctions.QueryAsync(
                    $"SELECT {PermissionColumns} {PermissionFrom} WHERE (@inc = 1 OR (p.IsActive = 1 AND m.IsActive = 1)) ORDER BY p.Code",
                    MapPermission, new[] { P("@inc", _includeInactive) }, _ct);

                ILookup<long, PermissionDto> _byModule = _permissions.ToLookup(p => p.ModuleId);
                foreach (ModuleDto _module in _modules) _module.Permissions = _byModule[_module.Id].ToList();
                return (_modules.ToList(), new FunctionReturn { MethodName = _methodName });
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
