using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.Models;
using SageAdminApi.AppValidations;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Admin dashboard: the same lead figures as the User Panel's Home (dbo.usp_Lead_Summary), for every lead or for one
    /// sales user (the leads they created, generated or are assigned to).
    /// </summary>
    public class LeadSummaryFunctions : ILeadSummaryFunctions
    {
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        public LeadSummaryFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
        }

        public async Task<(LeadSummary summary, IFunctionReturn functionReturn)> GetSummaryAsync(Guid? _salesUserId, CancellationToken _ct = default)
        {
            string _methodName = "F:LeadSummary:GetSummary";
            try
            {
                long _actorId = 0;
                if (_salesUserId is Guid _publicId)
                {
                    (IFunctionReturn _lookup, DataTable _users) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                        " SELECT Id FROM dbo.SalesUsers WHERE PublicId = @id ", new[] { P("@id", _publicId) }, _methodName, _ct);
                    if (!_lookup.Status) return (null, _lookup);
                    DataRow _user = _users.FirstRow();
                    if (_user == null) return (null, ApplicationCommonFunctions.NotFound("Sales user not found.", _methodName));
                    _actorId = _user.GetLong("Id");
                }

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.LeadSummary, new[]
                {
                    P("@ActorSalesUserId", _actorId), P("@Mine", _salesUserId.HasValue)
                }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                var _summary = new LeadSummary();
                foreach (DataRow _row in _dataTable.Rows)
                {
                    var _item = new LeadSummaryItem
                    {
                        Id = _row.GetInt("Id"), Code = _row.GetText("Code"), Name = _row.GetText("Name"),
                        Total = _row.GetInt("Total"), Won = _row.GetInt("Won"), Revenue = _row.GetDecimal("Revenue")
                    };
                    switch (_row.GetText("Section"))
                    {
                        case "Total":
                            _summary.TotalLeads = _item.Total;
                            _summary.WonLeads = _item.Won;
                            _summary.WonRevenueMonthly = _item.Revenue;
                            break;
                        case "Status": _summary.Statuses.Add(_item); break;
                        case "Source": _summary.Sources.Add(_item); break;
                        case "Aging": _summary.Aging.Add(_item); break;
                        case "Type": _summary.Types.Add(_item); break;
                        case "Product": _summary.Products.Add(_item); break;
                    }
                }
                _summary.Statuses = _summary.Statuses.OrderBy(x => x.Id).ToList();
                _summary.Aging = _summary.Aging.OrderBy(x => x.Id).ToList();
                _summary.Sources = _summary.Sources.OrderByDescending(x => x.Total).ThenBy(x => x.Name).ToList();
                _summary.Products = _summary.Products.OrderByDescending(x => x.Revenue).ThenBy(x => x.Name).ToList();
                _summary.ConversionRate = _summary.TotalLeads > 0 ? decimal.Round(_summary.WonLeads * 100m / _summary.TotalLeads, 2) : 0m;
                _summary.WonRevenueAnnualized = _summary.WonRevenueMonthly * 12;
                return (_summary, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
