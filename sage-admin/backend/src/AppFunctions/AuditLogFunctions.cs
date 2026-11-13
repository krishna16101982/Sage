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
using SageAdminApi.Models;
using SageAdminApi.Models.Filter;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// The audit log screen: every recorded change and sign-in, newest first, with filters, and one entry's before/after
    /// values. Read-only: entries are written by IAuditFunctions (Admin API) and by the User API.
    /// </summary>
    public class AuditLogFunctions : IAuditLogFunctions
    {
        private const string DetailSelect = @"SELECT a.Id, a.CreatedAtUtc,
    CASE WHEN a.ActorUserId IS NOT NULL THEN 'admin' WHEN ISJSON(a.NewValues) = 1 AND JSON_VALUE(a.NewValues, '$.By') = 'SalesUser' THEN 'sales' ELSE 'system' END,
    CASE WHEN u.Id IS NOT NULL THEN CONCAT(u.FirstName, N' ', u.LastName) WHEN ISJSON(a.NewValues) = 1 THEN JSON_VALUE(a.NewValues, '$.SalesUserName') END,
    u.Username, a.Action, a.EntityType, a.EntityId, a.IpAddress,
    CASE WHEN a.OldValues IS NULL THEN 0 ELSE 1 END, CASE WHEN a.NewValues IS NULL THEN 0 ELSE 1 END,
    a.OldValues, a.NewValues
FROM dbo.AuditLogs a LEFT JOIN dbo.AdminUsers u ON u.Id = a.ActorUserId
WHERE a.Id = @id";

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        public AuditLogFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
        }

        #region Helpers

        private static SqlParameter P(string _name, object _value) => new SqlParameter(_name, _value ?? DBNull.Value);
        private static SqlParameter Date(string _name, DateOnly? _value)
            => new SqlParameter(_name, SqlDbType.Date) { Value = _value.HasValue ? _value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value };
        private static string S(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

        private static T Fill<T>(SqlDataReader r, T _item) where T : AuditLogItem
        {
            _item.Id = r.GetInt64(0);
            _item.CreatedAtUtc = DateTime.SpecifyKind(r.GetDateTime(1), DateTimeKind.Utc);
            _item.ActorType = r.GetString(2);
            _item.ActorName = S(r, 3);
            _item.ActorUsername = S(r, 4);
            _item.Action = r.GetString(5);
            _item.EntityType = r.GetString(6);
            _item.EntityId = S(r, 7);
            _item.IpAddress = S(r, 8);
            _item.HasOldValues = r.GetInt32(9) == 1;
            _item.HasNewValues = r.GetInt32(10) == 1;
            return _item;
        }

        /// <summary>Stored JSON as a JSON value (null when empty or not valid JSON).</summary>
        private static JsonElement? Json(SqlDataReader r, int i)
        {
            if (r.IsDBNull(i)) return null;
            try
            {
                using JsonDocument _doc = JsonDocument.Parse(r.GetString(i));
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
                var _totalCount = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };

                IReadOnlyList<AuditLogItem> _items = await _databaseFunctions.QueryProcedureAsync(SP.AuditLogGetPaged, r => Fill(r, new AuditLogItem()), new List<SqlParameter>
                {
                    P("@Search", _search), P("@EntityType", _filter.EntityType.NullIfWhiteSpace()), P("@Action", _filter.Action.NullIfWhiteSpace()),
                    P("@ActorType", _actorType), P("@ActorPublicId", _filter.ActorId), Date("@From", _filter.From), Date("@To", _filter.To),
                    P("@SortDirection", _paging.SortDirection), P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                }, _ct);

                int _total = _totalCount.Value is int _count ? _count : 0;
                return (new PagedResult<AuditLogItem> { Items = _items, Page = _paging.Page, PageSize = _paging.PageSize, TotalCount = _total }, new FunctionReturn { MethodName = _methodName });
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
                AuditLogDetail _entry = await _databaseFunctions.QuerySingleOrDefaultAsync(DetailSelect, r =>
                {
                    AuditLogDetail _d = Fill(r, new AuditLogDetail());
                    _d.OldValues = Json(r, 11);
                    _d.NewValues = Json(r, 12);
                    return _d;
                }, new[] { P("@id", _id) }, _ct);
                return _entry == null
                    ? (null, ApplicationCommonFunctions.NotFound("Audit entry not found.", _methodName))
                    : (_entry, new FunctionReturn { MethodName = _methodName });
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
                var _types = await _databaseFunctions.QueryAsync("SELECT DISTINCT EntityType FROM dbo.AuditLogs ORDER BY EntityType", r => r.GetString(0), null, _ct);
                var _actions = await _databaseFunctions.QueryAsync("SELECT DISTINCT Action FROM dbo.AuditLogs ORDER BY Action", r => r.GetString(0), null, _ct);
                var _admins = await _databaseFunctions.QueryAsync(
                    @"SELECT u.PublicId, CONCAT(u.FirstName, N' ', u.LastName) FROM dbo.AdminUsers u
                      WHERE EXISTS (SELECT 1 FROM dbo.AuditLogs a WHERE a.ActorUserId = u.Id) ORDER BY u.FirstName, u.LastName",
                    r => new AuditActorOption { Id = r.GetGuid(0), Name = r.GetString(1) }, null, _ct);
                return (new AuditLogOptions { EntityTypes = _types.ToList(), Actions = _actions.ToList(), Admins = _admins.ToList() }, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
