using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Models;
using SageAdminApi.Models.Filter;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// The audit log screen: every recorded change and sign-in, newest first, with filters, and one entry's before/after
    /// values. Read-only: entries are written by IAuditFunctions (Admin API) and by the User API.
    /// </summary>
    public class AuditLogFunctions : IAuditLogFunctions
    {
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        public AuditLogFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
        }

        #region Helpers

        /// <summary>One audit row (from dbo.usp_AuditLog_GetPaged or GetAuditLogAsync; both use these column names).</summary>
        private static T Fill<T>(DataRow _row, T _item) where T : AuditLogItem
        {
            _item.Id = _row.GetLong("Id");
            _item.CreatedAtUtc = _row.GetUtc("CreatedAtUtc");
            _item.ActorType = _row.GetText("ActorType");
            _item.ActorName = _row.GetText("ActorName");
            _item.ActorUsername = _row.GetText("ActorUsername");
            _item.Action = _row.GetText("Action");
            _item.EntityType = _row.GetText("EntityType");
            _item.EntityId = _row.GetText("EntityId");
            _item.IpAddress = _row.GetText("IpAddress");
            _item.HasOldValues = _row.GetInt("HasOld") == 1;
            _item.HasNewValues = _row.GetInt("HasNew") == 1;
            return _item;
        }

        /// <summary>Stored JSON as a JSON value (null when empty or not valid JSON).</summary>
        private static JsonElement? Json(DataRow _row, string _column)
        {
            string _text = _row.GetText(_column);
            if (_text == null) return null;
            try
            {
                using JsonDocument _doc = JsonDocument.Parse(_text);
                return _doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        #endregion

        public async Task<(PagedResult<AuditLogItem> result, IFunctionReturn functionReturn)> GetAuditLogsAsync(AuditLogFilter _filter, CancellationToken _ct = default)
        {
            string _methodName = "F:AuditLog:GetAuditLogs";
            try
            {
                _filter ??= new AuditLogFilter();
                PaginationParams _paging = new Pagination().GetPagingParams(_filter, new[] { "createdAtUtc" }, "createdAtUtc");
                if (_filter.PageSize <= 0) _paging.PageSize = 50;
                string _actorType = _filter.ActorType == "admin" || _filter.ActorType == "sales" || _filter.ActorType == "system" ? _filter.ActorType : null;
                string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
                var _totalCount = IntOut("@TotalCount");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.AuditLogGetPaged, new[]
                {
                    P("@Search", _search), P("@EntityType", _filter.EntityType.NullIfWhiteSpace()), P("@Action", _filter.Action.NullIfWhiteSpace()),
                    P("@ActorType", _actorType), P("@ActorPublicId", _filter.ActorId), Date("@From", _filter.From), Date("@To", _filter.To),
                    P("@SortDirection", _paging.SortDirection), P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                return (new PagedResult<AuditLogItem>
                {
                    Items = _dataTable.MapRows(_row => Fill(_row, new AuditLogItem())),
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

        public async Task<(AuditLogDetail entry, IFunctionReturn functionReturn)> GetAuditLogAsync(long _id, CancellationToken _ct = default)
        {
            string _methodName = "F:AuditLog:GetAuditLog";
            try
            {
                // Same column names as dbo.usp_AuditLog_GetPaged, plus the before / after values.
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT a.Id, a.CreatedAtUtc, ");
                _sqlQuery.Append("        CASE WHEN a.ActorUserId IS NOT NULL THEN 'admin' ");
                _sqlQuery.Append("             WHEN ISJSON(a.NewValues) = 1 AND JSON_VALUE(a.NewValues, '$.By') = 'SalesUser' THEN 'sales' ");
                _sqlQuery.Append("             ELSE 'system' END AS ActorType, ");
                _sqlQuery.Append("        CASE WHEN u.Id IS NOT NULL THEN CONCAT(u.FirstName, N' ', u.LastName) ");
                _sqlQuery.Append("             WHEN ISJSON(a.NewValues) = 1 THEN JSON_VALUE(a.NewValues, '$.SalesUserName') END AS ActorName, ");
                _sqlQuery.Append("        u.Username AS ActorUsername, a.Action, a.EntityType, a.EntityId, a.IpAddress, ");
                _sqlQuery.Append("        CASE WHEN a.OldValues IS NULL THEN 0 ELSE 1 END AS HasOld, ");
                _sqlQuery.Append("        CASE WHEN a.NewValues IS NULL THEN 0 ELSE 1 END AS HasNew, ");
                _sqlQuery.Append("        a.OldValues, a.NewValues ");
                _sqlQuery.Append(" FROM dbo.AuditLogs a ");
                _sqlQuery.Append(" LEFT JOIN dbo.AdminUsers u ON u.Id = a.ActorUserId ");
                _sqlQuery.Append(" WHERE a.Id = @id ");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                    _sqlQuery.ToString(), new[] { P("@id", _id) }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                DataRow _row = _dataTable.FirstRow();
                if (_row == null) return (null, ApplicationCommonFunctions.NotFound("Audit entry not found.", _methodName));

                AuditLogDetail _entry = Fill(_row, new AuditLogDetail());
                _entry.OldValues = Json(_row, "OldValues");
                _entry.NewValues = Json(_row, "NewValues");
                return (_entry, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(AuditLogOptions options, IFunctionReturn functionReturn)> GetOptionsAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:AuditLog:GetOptions";
            try
            {
                // Three small lists for the filters, in one round trip each.
                (IFunctionReturn _typesReturn, DataTable _types) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                    " SELECT DISTINCT EntityType FROM dbo.AuditLogs ORDER BY EntityType ", null, _methodName, _ct);
                if (!_typesReturn.Status) return (null, _typesReturn);

                (IFunctionReturn _actionsReturn, DataTable _actions) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                    " SELECT DISTINCT Action FROM dbo.AuditLogs ORDER BY Action ", null, _methodName, _ct);
                if (!_actionsReturn.Status) return (null, _actionsReturn);

                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT u.PublicId, CONCAT(u.FirstName, N' ', u.LastName) AS Name ");
                _sqlQuery.Append(" FROM dbo.AdminUsers u ");
                _sqlQuery.Append(" WHERE EXISTS (SELECT 1 FROM dbo.AuditLogs a WHERE a.ActorUserId = u.Id) ");
                _sqlQuery.Append(" ORDER BY u.FirstName, u.LastName ");
                (IFunctionReturn _functionReturn, DataTable _admins) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), null, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                return (new AuditLogOptions
                {
                    EntityTypes = _types.MapRows(_row => _row.GetText("EntityType")),
                    Actions = _actions.MapRows(_row => _row.GetText("Action")),
                    Admins = _admins.MapRows(_row => new AuditActorOption { Id = _row.GetGuid("PublicId"), Name = _row.GetText("Name") })
                }, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
