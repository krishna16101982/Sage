using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Order form lists (dbo.MasterValues): billing frequency, billing method, payment method and payment term. Admins add
    /// values, rename them, reorder them, turn them off and choose each list's default (filled in on a new order form).
    /// </summary>
    public partial class MasterValueFunctions : IMasterValueFunctions
    {
        /// <summary>The lists, in screen order, with their labels.</summary>
        public static readonly (string Type, string Label)[] Lists =
        {
            ("BillingFrequency", "Billing frequency"), ("BillingMethod", "Billing method"),
            ("PaymentMethod", "Payment method"), ("PaymentTerm", "Payment term"), ("Currency", "Currency")
        };

        [GeneratedRegex("^[A-Z0-9_]{1,30}$")]
        private static partial Regex CodeRegex();

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly IAuditFunctions _auditFunctions;

        public MasterValueFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IAuditFunctions __auditFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _auditFunctions = __auditFunctions;
        }

        #region Helpers

        private static MasterValueDto Map(DataRow _row) => new MasterValueDto
        {
            Id = _row.GetInt("Id"),
            MasterType = _row.GetText("MasterType"),
            Code = _row.GetText("Code"),
            Name = _row.GetText("Name"),
            SortOrder = _row.GetInt("SortOrder"),
            IsDefault = _row.GetBool("IsDefault"),
            IsActive = _row.GetBool("IsActive"),
            UsedBy = _row.GetInt("UsedBy"),
            UpdatedAtUtc = _row.GetNullableUtc("UpdatedAtUtc")
        };

        /// <summary>Every value (id = null) or one value, with how many orders use it.</summary>
        private async Task<(IFunctionReturn functionReturn, List<MasterValueDto> values)> ReadAsync(int? _id, string _methodName, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT m.Id, m.MasterType, m.Code, m.Name, m.SortOrder, m.IsDefault, m.IsActive, m.UpdatedAtUtc, ");
            _sqlQuery.Append("        (SELECT COUNT(*) FROM dbo.Sales s WHERE s.BillingFrequencyId = m.Id OR s.BillingMethodId = m.Id ");
            _sqlQuery.Append("                                             OR s.PaymentMethodId = m.Id OR s.PaymentTermId = m.Id OR s.CurrencyId = m.Id) AS UsedBy ");
            _sqlQuery.Append(" FROM dbo.MasterValues m ");
            if (_id.HasValue) _sqlQuery.Append(" WHERE m.Id = @id ");
            _sqlQuery.Append(" ORDER BY m.MasterType, m.SortOrder, m.Name ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), _id.HasValue ? new[] { P("@id", _id.Value) } : null, _methodName, _ct);
            return (_functionReturn, _dataTable.MapRows(Map));
        }

        private static IFunctionReturn Invalid(List<string> _errors, string _methodName)
            => new FunctionReturn { Status = false, MessageType = Constants.ReturnMessageTypes.ValidationError, Message = _errors, HttpStatusCode = Constants.HttpStatusCodes.BadRequest, MethodName = _methodName };

        private static object Snapshot(MasterValueDto v) => v == null ? null : new { v.MasterType, v.Code, v.Name, v.SortOrder, v.IsDefault, v.IsActive };

        #endregion

        public async Task<(List<MasterList> lists, IFunctionReturn functionReturn)> GetListsAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:MasterValue:GetLists";
            try
            {
                (IFunctionReturn _functionReturn, List<MasterValueDto> _values) = await ReadAsync(null, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                List<MasterList> _lists = Lists.Select(l => new MasterList
                {
                    MasterType = l.Type,
                    Label = l.Label,
                    Values = _values.Where(v => v.MasterType == l.Type).ToList()
                }).ToList();
                return (_lists, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(MasterValueDto value, IFunctionReturn functionReturn)> SaveAsync(int? _id, HttpMasterValuePostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = _id.HasValue ? "F:MasterValue:Update" : "F:MasterValue:Create";
            try
            {
                _params ??= new HttpMasterValuePostParams();
                string _type = _params.MasterType?.Trim();
                string _code = _params.Code?.Trim().ToUpperInvariant();
                string _name = Clean(_params.Name);
                var _errors = new List<string>();
                if (!Lists.Any(l => l.Type == _type)) _errors.Add("Choose a list.");
                if (string.IsNullOrEmpty(_code) || !CodeRegex().IsMatch(_code)) _errors.Add("Code: 1 to 30 capital letters, digits or _.");
                if (string.IsNullOrEmpty(_name) || _name.Length > 100) _errors.Add("Name is required (up to 100 characters).");
                if (_params.SortOrder < 0 || _params.SortOrder > 10000) _errors.Add("Order must be between 0 and 10000.");
                if (_errors.Count > 0) return (null, Invalid(_errors, _methodName));

                MasterValueDto _existing = null;
                if (_id.HasValue)
                {
                    (IFunctionReturn _read, List<MasterValueDto> _found) = await ReadAsync(_id, _methodName, _ct);
                    if (!_read.Status) return (null, _read);
                    _existing = _found.FirstOrDefault();
                    if (_existing == null) return (null, ApplicationCommonFunctions.NotFound("List value not found.", _methodName));
                }

                (IFunctionReturn _functionReturn, long _newId) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.MasterValueSave, new[]
                {
                    P("@Id", _id), P("@MasterType", _type), P("@Code", _code), P("@Name", _name), P("@SortOrder", _params.SortOrder),
                    P("@IsDefault", _params.IsDefault), P("@IsActive", _params.IsActive), P("@ActorUserId", _actorUserId)
                }, _methodName, _id.HasValue ? "List value saved." : "List value added.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                (IFunctionReturn _reread, List<MasterValueDto> _after) = await ReadAsync((int)_newId, _methodName, _ct);
                EnsureRead(_reread);
                MasterValueDto _saved = _after[0];
                await _auditFunctions.LogAsync(_actorUserId, (_id.HasValue ? ApplicationEnumeration.AuditAction.Update : ApplicationEnumeration.AuditAction.Create).ToString(),
                    ApplicationEnumeration.ObjectType.MasterValue.ToString(), _saved.Id.ToString(), Snapshot(_existing), Snapshot(_saved), _ipAddress, _ct);
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
