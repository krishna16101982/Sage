using System;
using System.Collections.Generic;
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

namespace SageAdminApi.AppFunctions
{
    public class PermissionFunctions : IPermissionFunctions
    {
        private const string PermissionColumns = "p.Id, p.ModuleId, m.Code, m.Name, p.Action, p.Code, p.Name, p.Description, p.IsActive, p.IsSystem";
        private const string PermissionFrom = "FROM dbo.Permissions p JOIN dbo.Modules m ON m.Id = p.ModuleId";
        private const string ModuleSelect = "SELECT m.Id, m.Code, m.Name, m.Category, m.SortOrder, m.IsActive, m.IsSystem, (SELECT COUNT(*) FROM dbo.Permissions px WHERE px.ModuleId = m.Id) FROM dbo.Modules m";

        /// <summary>
        /// Sort keys the client may request mapped to fixed SQL. Client text is never concatenated into SQL.
        /// </summary>
        private static readonly Dictionary<string, string> SortExpressions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "module", "m.SortOrder {0}, m.Name {0}, p.Code" },
            { "code", "p.Code {0}" },
            { "name", "p.Name {0}, p.Code" },
            { "action", "p.Action {0}, p.Code" },
            { "isActive", "p.IsActive {0}, p.Code" },
            { "createdAtUtc", "p.CreatedAtUtc {0}, p.Id" }
        };

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
            PermissionCount = r.GetInt32(7)
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

        private static string Clean(string _value) => string.IsNullOrWhiteSpace(_value) ? null : Sanitization.Sanitize(_value.Trim());

        private Task<ModuleDto> GetModuleByIdAsync(long _id, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync(ModuleSelect + " WHERE m.Id = @id", MapModule, new[] { P("@id", _id) }, _ct);

        private Task<PermissionDto> GetPermissionByIdAsync(long _id, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync($"SELECT {PermissionColumns} {PermissionFrom} WHERE p.Id = @id", MapPermission, new[] { P("@id", _id) }, _ct);

        /// <summary>Audit snapshot: only business fields, never secrets.</summary>
        private static object Snapshot(ModuleDto m) => m == null ? null : new { m.Code, m.Name, m.Category, m.SortOrder, m.IsActive };
        private static object Snapshot(PermissionDto p) => p == null ? null : new { p.Code, p.Name, p.Description, p.IsActive };

        #endregion

        public async Task<(PagedResult<PermissionDto> result, IFunctionReturn functionReturn)> GetPermissionsAsync(PermissionFilter _filter, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Permission:GetPermissions";
            StringBuilder _sqlQuery;
            #endregion
            try
            {
                _filter = _filter ?? new PermissionFilter();
                PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortExpressions.Keys, "module");
                // Default direction for browsing the catalogue is ascending.
                if (string.IsNullOrWhiteSpace(_filter.SortDirection)) _paging.SortDirection = "ASC";

                var _where = new StringBuilder(" WHERE 1 = 1");
                var _parameters = new List<SqlParameter>();
                if (_filter.ModuleId.HasValue) { _where.Append(" AND p.ModuleId = @moduleId"); _parameters.Add(P("@moduleId", _filter.ModuleId.Value)); }
                if (_filter.IsActive.HasValue) { _where.Append(" AND p.IsActive = @isActive"); _parameters.Add(P("@isActive", _filter.IsActive.Value)); }
                if (!string.IsNullOrWhiteSpace(_filter.Search))
                {
                    _where.Append(" AND (p.Code LIKE @search ESCAPE '\\' OR p.Name LIKE @search ESCAPE '\\' OR m.Name LIKE @search ESCAPE '\\')");
                    _parameters.Add(P("@search", "%" + _filter.Search.Trim().EscapeLike() + "%"));
                }

                // Total rows. (The wrappers copy the parameter list per command, so one list can be reused safely.)
                _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT COUNT(*) ").Append(PermissionFrom).Append(_where);
                int _total = await _databaseFunctions.ExecuteScalarAsync<int>(_sqlQuery.ToString(), _parameters.Select(x => new SqlParameter(x.ParameterName, x.Value)).ToList(), _ct);

                // One page
                string _orderBy = string.Format(SortExpressions[_paging.SortBy], _paging.SortDirection);
                _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT ").Append(PermissionColumns).Append(' ').Append(PermissionFrom).Append(_where);
                _sqlQuery.Append(" ORDER BY ").Append(_orderBy);
                _sqlQuery.Append(" OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY ");
                _parameters.Add(P("@offset", _paging.Offset));
                _parameters.Add(P("@size", _paging.PageSize));
                IReadOnlyList<PermissionDto> _items = await _databaseFunctions.QueryAsync(_sqlQuery.ToString(), MapPermission, _parameters, _ct);

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
            StringBuilder _sqlQuery;
            IFunctionReturn _functionReturn;
            long _id;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateModule(_params?.Code, _params?.Name, _params?.Category, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                // INSERT (blocked with a 409 when the code already exists)
                _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" INSERT dbo.Modules (Code, Name, Category, SortOrder, IsActive, IsSystem) ");
                _sqlQuery.Append(" VALUES (@code, @name, @category, @sortOrder, 1, 0) ");
                var _parameters = new[] { P("@code", _params.Code.Trim()), P("@name", Clean(_params.Name)), P("@category", Clean(_params.Category)), P("@sortOrder", _params.SortOrder) };

                (_functionReturn, _id) = await _databaseFunctions.ExecuteCheckExistsAndInsertAsync(
                    "SELECT COUNT(*) FROM dbo.Modules WHERE Code = @code", _sqlQuery.ToString(), _parameters, _methodName,
                    ApplicationConstants.ValidationMessages.ModuleCodeExists, "Module created.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

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
            StringBuilder _sqlQuery;
            IFunctionReturn _functionReturn;
            long _retValue;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateModuleUpdate(_params?.Name, _params?.Category, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ModuleDto _existing = await GetModuleByIdAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.ModuleNotFound, _methodName));
                if (_existing.IsSystem && !_params.IsActive) return (null, ApplicationCommonFunctions.BadRequest(ApplicationConstants.ValidationMessages.SystemModuleDeactivate, _methodName));

                bool _activeChanged = _existing.IsActive != _params.IsActive;

                // UPDATE. Changing a module's active flag changes effective access: bump the permission version of every
                // user who holds one of its permissions so their tokens are re-evaluated on the next request.
                _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SET XACT_ABORT ON; BEGIN TRAN; ");
                _sqlQuery.Append(" UPDATE dbo.Modules SET Name = @name, Category = @category, SortOrder = @sortOrder, IsActive = @isActive, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @id; ");
                _sqlQuery.Append(" IF @changed = 1 ");
                _sqlQuery.Append("   UPDATE dbo.AdminUsers SET PermissionVersion = PermissionVersion + 1 ");
                _sqlQuery.Append("   WHERE Id IN (SELECT ug.UserId FROM dbo.AdminUserGroups ug ");
                _sqlQuery.Append("                JOIN dbo.GroupPermissions gp ON gp.GroupId = ug.GroupId ");
                _sqlQuery.Append("                JOIN dbo.Permissions p ON p.Id = gp.PermissionId WHERE p.ModuleId = @id); ");
                _sqlQuery.Append(" COMMIT; ");

                (_functionReturn, _retValue) = await _databaseFunctions.ExecuteUpdateAsync(_sqlQuery.ToString(), new[]
                {
                    P("@name", Clean(_params.Name)), P("@category", Clean(_params.Category)), P("@sortOrder", _params.SortOrder),
                    P("@isActive", _params.IsActive), P("@id", _id), P("@changed", _activeChanged)
                }, _methodName, "Module updated.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

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
            StringBuilder _sqlQuery;
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

                // INSERT (blocked with a 409 when Module.Action already exists)
                string _action = _params.Action.Trim();
                _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" INSERT dbo.Permissions (ModuleId, Action, Code, Name, Description, IsActive, IsSystem) ");
                _sqlQuery.Append(" VALUES (@moduleId, @action, @code, @name, @description, 1, 0) ");
                var _parameters = new[]
                {
                    P("@moduleId", _module.Id), P("@action", _action), P("@code", _module.Code + "." + _action),
                    P("@name", Clean(_params.Name)), P("@description", Clean(_params.Description))
                };

                (_functionReturn, _id) = await _databaseFunctions.ExecuteCheckExistsAndInsertAsync(
                    "SELECT COUNT(*) FROM dbo.Permissions WHERE Code = @code", _sqlQuery.ToString(), _parameters, _methodName,
                    ApplicationConstants.ValidationMessages.PermissionCodeExists, "Permission created.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

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
            StringBuilder _sqlQuery;
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

                // UPDATE (+ permission-version bump for users holding it when the active flag changes)
                _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SET XACT_ABORT ON; BEGIN TRAN; ");
                _sqlQuery.Append(" UPDATE dbo.Permissions SET Name = @name, Description = @description, IsActive = @isActive, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @id; ");
                _sqlQuery.Append(" IF @changed = 1 ");
                _sqlQuery.Append("   UPDATE dbo.AdminUsers SET PermissionVersion = PermissionVersion + 1 ");
                _sqlQuery.Append("   WHERE Id IN (SELECT ug.UserId FROM dbo.AdminUserGroups ug JOIN dbo.GroupPermissions gp ON gp.GroupId = ug.GroupId WHERE gp.PermissionId = @id); ");
                _sqlQuery.Append(" COMMIT; ");

                (_functionReturn, _retValue) = await _databaseFunctions.ExecuteUpdateAsync(_sqlQuery.ToString(), new[]
                {
                    P("@name", Clean(_params.Name)), P("@description", Clean(_params.Description)), P("@isActive", _params.IsActive), P("@id", _id), P("@changed", _activeChanged)
                }, _methodName, "Permission updated.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

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
