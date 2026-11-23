using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Lead lists (DDL 035), one table each: products, lead sources and billing cycles of the User Panel's lead form.
    /// Admins add values, rename them, reorder them and turn them off. Lead status and lead type are fixed lists.
    /// </summary>
    public class LeadListFunctions : ILeadListFunctions
    {
        /// <summary>The lists, in screen order: code, label, table (fixed names, never from input).</summary>
        public static readonly (string List, string Label, string Table, string LeadColumn)[] Lists =
        {
            ("Product", "Products", "dbo.LeadProducts", "ProductId"),
            ("Source", "Lead sources", "dbo.LeadSources", "SourceId"),
            ("BillingCycle", "Billing cycles", "dbo.LeadBillingCycles", "BillingCycleId")
        };

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly IAuditFunctions _auditFunctions;

        public LeadListFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IAuditFunctions __auditFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _auditFunctions = __auditFunctions;
        }

        #region Helpers

        /// <summary>Every value of every list (_list = null) or one value, with how many leads use it.</summary>
        private async Task<(IFunctionReturn functionReturn, List<LeadListValueDto> values)> ReadAsync(string _list, int? _id, string _methodName, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            bool _first = true;
            foreach (var l in Lists.Where(x => _list == null || x.List == _list))
            {
                if (!_first) _sqlQuery.Append(" UNION ALL ");
                _first = false;
                _sqlQuery.Append($" SELECT '{l.List}' AS List, v.Id, v.Name, v.SortOrder, v.IsActive, v.UpdatedAtUtc, ");
                _sqlQuery.Append($"        (SELECT COUNT(*) FROM dbo.Leads ld WHERE ld.{l.LeadColumn} = v.Id AND ld.IsDeleted = 0) AS UsedBy ");
                _sqlQuery.Append($" FROM {l.Table} v ");
                if (_id.HasValue) _sqlQuery.Append(" WHERE v.Id = @id ");
            }
            _sqlQuery.Append(" ORDER BY List, SortOrder, Name ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), _id.HasValue ? new[] { P("@id", _id.Value) } : null, _methodName, _ct);
            return (_functionReturn, _dataTable.MapRows(_row => new LeadListValueDto
            {
                List = _row.GetText("List"),
                Id = _row.GetInt("Id"),
                Name = _row.GetText("Name"),
                SortOrder = _row.GetInt("SortOrder"),
                IsActive = _row.GetBool("IsActive"),
                UsedBy = _row.GetInt("UsedBy"),
                UpdatedAtUtc = _row.GetNullableUtc("UpdatedAtUtc")
            }));
        }

        private static IFunctionReturn Invalid(List<string> _errors, string _methodName)
            => new FunctionReturn { Status = false, MessageType = Constants.ReturnMessageTypes.ValidationError, Message = _errors, HttpStatusCode = Constants.HttpStatusCodes.BadRequest, MethodName = _methodName };

        private static object Snapshot(LeadListValueDto v) => v == null ? null : new { v.List, v.Name, v.SortOrder, v.IsActive };

        #endregion

        public async Task<(List<LeadList> lists, IFunctionReturn functionReturn)> GetListsAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:LeadList:GetLists";
            try
            {
                (IFunctionReturn _functionReturn, List<LeadListValueDto> _values) = await ReadAsync(null, null, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                return (Lists.Select(l => new LeadList { List = l.List, Label = l.Label, Values = _values.Where(v => v.List == l.List).ToList() }).ToList(), _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(LeadListValueDto value, IFunctionReturn functionReturn)> SaveAsync(int? _id, HttpLeadListValuePostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = _id.HasValue ? "F:LeadList:Update" : "F:LeadList:Create";
            try
            {
                _params ??= new HttpLeadListValuePostParams();
                string _list = _params.List?.Trim();
                string _name = Clean(_params.Name);
                var _errors = new List<string>();
                if (!Lists.Any(l => l.List == _list)) _errors.Add("Choose a list.");
                if (string.IsNullOrEmpty(_name) || _name.Length > 100) _errors.Add("Name is required (up to 100 characters).");
                if (_params.SortOrder < 0 || _params.SortOrder > 10000) _errors.Add("Order must be between 0 and 10000.");
                if (_errors.Count > 0) return (null, Invalid(_errors, _methodName));

                LeadListValueDto _existing = null;
                if (_id.HasValue)
                {
                    (IFunctionReturn _read, List<LeadListValueDto> _found) = await ReadAsync(_list, _id, _methodName, _ct);
                    if (!_read.Status) return (null, _read);
                    _existing = _found.FirstOrDefault();
                    if (_existing == null) return (null, ApplicationCommonFunctions.NotFound("List value not found.", _methodName));
                }

                (IFunctionReturn _functionReturn, long _newId) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.LeadListValueSave, new[]
                {
                    P("@List", _list), P("@Id", _id), P("@Name", _name), P("@SortOrder", _params.SortOrder),
                    P("@IsActive", _params.IsActive), P("@ActorUserId", _actorUserId)
                }, _methodName, _id.HasValue ? "List value saved." : "List value added.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                (IFunctionReturn _reread, List<LeadListValueDto> _after) = await ReadAsync(_list, (int)_newId, _methodName, _ct);
                EnsureRead(_reread);
                LeadListValueDto _saved = _after[0];
                await _auditFunctions.LogAsync(_actorUserId, (_id.HasValue ? ApplicationEnumeration.AuditAction.Update : ApplicationEnumeration.AuditAction.Create).ToString(),
                    ApplicationEnumeration.ObjectType.LeadListValue.ToString(), $"{_saved.List}:{_saved.Id}", Snapshot(_existing), Snapshot(_saved), _ipAddress, _ct);
                if (!_id.HasValue) _functionReturn.HttpStatusCode = Constants.HttpStatusCodes.Created;
                return (_saved, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
