using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.AppValidations;
using SageUserApi.Models;
using SageUserApi.Models.Filter;
using SageUserApi.Models.HttpPost;
using SP = SageUserApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using static SageCommonLib.AppFunctions.Sql;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// Lead Records (DDL 035). Every sales user sees every lead; the creator and the creator's manager change it; managers
    /// delete it (hidden, never removed). Lists and paging come from dbo.usp_Lead_GetPaged, never from the browser.
    /// </summary>
    public class LeadFunctions : ILeadFunctions
    {
        private static readonly string[] SortKeys = { "leadNumber", "leadDate", "company", "revenue", "aging" };
        private static readonly string[] AgingBuckets = { "lt7", "7to30", "31to60", "gt60" };
        private const int MaxTextLength = 2000;
        /// <summary>Fixed rows of dbo.LeadStatuses / dbo.LeadTypes (DDL 035).</summary>
        private const int LeadStatusOpen = 1;
        private const int LeadTypeSelfWorking = 1;

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        public LeadFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
        }

        #region Helpers

        private static LeadDto Map(DataRow _row) => new LeadDto
        {
            Id = _row.GetGuid("PublicId"),
            LeadNumber = _row.GetText("LeadNumber"),
            CompanyId = _row.GetGuid("CustomerPublicId"),
            CompanyName = _row.GetText("CompanyName"),
            ClientName = _row.GetText("ClientName"),
            ClientDesignation = _row.GetText("ClientDesignation"),
            ClientEmail = _row.GetText("ClientEmail"),
            ClientPhone = _row.GetText("ClientPhone"),
            SecondaryClientName = _row.GetText("SecondaryClientName"),
            SecondaryClientDesignation = _row.GetText("SecondaryClientDesignation"),
            SecondaryClientEmail = _row.GetText("SecondaryClientEmail"),
            SecondaryClientPhone = _row.GetText("SecondaryClientPhone"),
            ProductId = _row.GetNullableInt("ProductId"),
            ProductName = _row.GetText("ProductName"),
            SourceId = _row.GetNullableInt("SourceId"),
            SourceName = _row.GetText("SourceName"),
            Requirement = _row.GetText("Requirement"),
            StatusId = _row.GetInt("StatusId"),
            StatusCode = _row.GetText("StatusCode"),
            StatusName = _row.GetText("StatusName"),
            TypeId = _row.GetInt("TypeId"),
            TypeName = _row.GetText("TypeName"),
            GeneratedById = _row.GetGuid("GeneratedByPublicId"),
            GeneratedByName = _row.GetText("GeneratedByName"),
            AssignedId = _row.GetGuid("AssignedPublicId"),
            AssignedName = _row.GetText("AssignedName"),
            MonthlyExpectedRevenue = _row.GetNullableDecimal("MonthlyExpectedRevenue"),
            BillingCycleId = _row.GetNullableInt("BillingCycleId"),
            BillingCycleName = _row.GetText("BillingCycleName"),
            NextFollowUpDate = _row.GetNullableDate("NextFollowUpDate"),
            LeadDate = _row.GetDate("LeadDate"),
            ClosingDate = _row.GetNullableDate("ClosingDate"),
            Remarks = _row.GetText("Remarks"),
            AgingDays = _row.GetInt("AgingDays"),
            SaleId = _row.GetNullableGuid("SalePublicId"),
            SaleNumber = _row.GetText("SaleNumber"),
            CreatedByName = _row.GetText("CreatedByName"),
            CreatedAtUtc = _row.GetUtc("CreatedAtUtc"),
            UpdatedAtUtc = _row.GetNullableUtc("UpdatedAtUtc"),
            CanEdit = _row.GetBool("CanEdit"),
            CanDelete = _row.GetBool("CanDelete"),
            CanOrder = _row.GetBool("CanOrder")
        };

        /// <summary>One lead (not deleted) with what _user may do with it, or null.</summary>
        private async Task<LeadDto> ReadOneAsync(Guid _id, SalesUserContext _user, CancellationToken _ct)
        {
            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.LeadGetPaged, new[]
            {
                P("@ActorSalesUserId", _user.Id), P("@LeadPublicId", _id), P("@Offset", 0), P("@PageSize", 1), IntOut("@TotalCount")
            }, "F:Lead:ReadOne", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            return _row == null ? null : Map(_row);
        }

        private static IFunctionReturn Invalid(List<string> _errors, string _methodName)
            => new FunctionReturn { Status = false, MessageType = Constants.ReturnMessageTypes.ValidationError, Message = _errors, HttpStatusCode = Constants.HttpStatusCodes.BadRequest, MethodName = _methodName };

        private static IFunctionReturn Forbidden(string _message, string _methodName)
            => FunctionReturn.Fail(Constants.ReturnMessageTypes.PermissionDenied, _message, Constants.HttpStatusCodes.Forbidden, _methodName);

        private static IFunctionReturn NotFound(string _methodName) => ApplicationCommonFunctions.NotFound("Lead not found.", _methodName);

        /// <summary>Trims and checks a contact; problems go to _errors.</summary>
        private static (string name, string designation, string email, string phone) Contact(string _name, string _designation, string _email, string _phone, string _who, List<string> _errors)
        {
            string _n = Clean(_name), _d = Clean(_designation), _e = _email.NullIfWhiteSpace(), _p = _phone.NullIfWhiteSpace();
            if (_n != null && _n.Length > 150) _errors.Add($"The {_who} name must be 150 characters or fewer.");
            if (_d != null && _d.Length > 100) _errors.Add($"The {_who} designation must be 100 characters or fewer.");
            if (_e != null && (!ValidationFunctions.IsValidEmail(_e) || _e.Length > 256)) _errors.Add($"Enter a valid {_who} email.");
            if (_p != null && !ValidationFunctions.IsValidPhone(_p)) _errors.Add($"Enter a 10-digit {_who} contact number.");
            return (_n, _d, _e, _p);
        }

        /// <summary>Checks the body and builds the parameters of dbo.usp_Lead_Save (everything but the lead id and actor).</summary>
        private static (List<SqlParameter> parameters, List<string> errors) Prepare(HttpLeadPostParams _params, Guid _companyId)
        {
            var _errors = new List<string>();
            var _primary = Contact(_params.ClientName, _params.ClientDesignation, _params.ClientEmail, _params.ClientPhone, "client", _errors);
            var _secondary = Contact(_params.SecondaryClientName, _params.SecondaryClientDesignation, _params.SecondaryClientEmail, _params.SecondaryClientPhone, "secondary client", _errors);
            string _requirement = Clean(_params.Requirement?.Replace("\r\n", "\n"));
            string _remarks = Clean(_params.Remarks?.Replace("\r\n", "\n"));
            if (_requirement != null && _requirement.Length > MaxTextLength) _errors.Add($"Requirement must be {MaxTextLength} characters or fewer.");
            if (_remarks != null && _remarks.Length > MaxTextLength) _errors.Add($"Remarks must be {MaxTextLength} characters or fewer.");
            if (_params.StatusId <= 0) _errors.Add("Choose the lead status.");
            if (_params.TypeId <= 0) _errors.Add("Choose the lead type.");
            if (!_params.GeneratedById.HasValue || _params.GeneratedById == Guid.Empty) _errors.Add("Choose who generated the lead.");
            if (!_params.AssignedId.HasValue || _params.AssignedId == Guid.Empty) _errors.Add("Choose the assigned person.");
            if (_params.MonthlyExpectedRevenue.HasValue && (_params.MonthlyExpectedRevenue < 0 || _params.MonthlyExpectedRevenue > 999_999_999_999m
                || decimal.Round(_params.MonthlyExpectedRevenue.Value, 2) != _params.MonthlyExpectedRevenue.Value))
                _errors.Add("Monthly expected revenue must be 0 or more, with at most 2 decimals.");
            if (!_params.LeadDate.HasValue) _errors.Add("Enter the created date.");
            if (_errors.Count > 0) return (null, _errors);

            return (new List<SqlParameter>
            {
                P("@CustomerPublicId", _companyId),
                P("@ClientName", _primary.name), P("@ClientDesignation", _primary.designation), P("@ClientEmail", _primary.email), P("@ClientPhone", _primary.phone),
                P("@SecondaryClientName", _secondary.name), P("@SecondaryClientDesignation", _secondary.designation),
                P("@SecondaryClientEmail", _secondary.email), P("@SecondaryClientPhone", _secondary.phone),
                P("@ProductId", _params.ProductId > 0 ? _params.ProductId : null), P("@SourceId", _params.SourceId > 0 ? _params.SourceId : null),
                P("@Requirement", _requirement), P("@StatusId", _params.StatusId), P("@TypeId", _params.TypeId),
                P("@GeneratedByPublicId", _params.GeneratedById), P("@AssignedPublicId", _params.AssignedId),
                Money("@MonthlyExpectedRevenue", _params.MonthlyExpectedRevenue), P("@BillingCycleId", _params.BillingCycleId > 0 ? _params.BillingCycleId : null),
                Date("@NextFollowUpDate", _params.NextFollowUpDate), Date("@LeadDate", _params.LeadDate), Date("@ClosingDate", _params.ClosingDate),
                P("@Remarks", _remarks)
            }, _errors);
        }

        private static object AuditOf(LeadDto l) => l == null ? null : new
        {
            l.LeadNumber, l.CompanyName, l.ClientName, l.ProductName, l.SourceName, l.StatusName, l.TypeName,
            l.GeneratedByName, l.AssignedName, l.MonthlyExpectedRevenue, l.BillingCycleName, l.NextFollowUpDate, l.LeadDate, l.ClosingDate
        };

        #endregion

        public async Task<(PagedResult<LeadDto> result, IFunctionReturn functionReturn)> GetLeadsAsync(LeadFilter _filter, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Lead:GetLeads";
            try
            {
                _filter ??= new LeadFilter();
                PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortKeys, "leadNumber");
                string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
                string _aging = Array.IndexOf(AgingBuckets, _filter.Aging) >= 0 ? _filter.Aging : null;
                var _totalCount = IntOut("@TotalCount");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.LeadGetPaged, new[]
                {
                    P("@ActorSalesUserId", _user.Id), P("@Search", _search), P("@StatusId", _filter.StatusId), P("@ProductId", _filter.ProductId),
                    P("@AssignedPublicId", _filter.AssignedId), P("@GeneratedByPublicId", _filter.GeneratedById), P("@Aging", _aging),
                    Date("@From", _filter.From), Date("@To", _filter.To), P("@WithoutOrder", _filter.WithoutOrder),
                    P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection), P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize),
                    _totalCount
                }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                return (new PagedResult<LeadDto>
                {
                    Items = _dataTable.MapRows(Map),
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

        public async Task<(LeadSummary summary, IFunctionReturn functionReturn)> GetSummaryAsync(bool _mine, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Lead:GetSummary";
            try
            {
                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.LeadSummary, new[]
                {
                    P("@ActorSalesUserId", _user.Id), P("@Mine", _mine)
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

        public async Task<(LeadDto lead, IFunctionReturn functionReturn)> GetLeadAsync(Guid _id, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Lead:GetLead";
            try
            {
                LeadDto _lead = await ReadOneAsync(_id, _user, _ct);
                return _lead == null ? (null, NotFound(_methodName)) : (_lead, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(LeadOptions options, IFunctionReturn functionReturn)> GetOptionsAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:Lead:GetOptions";
            try
            {
                var _options = new LeadOptions();
                async Task<DataTable> Read(string _sql)
                {
                    (IFunctionReturn _read, DataTable _table) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sql, null, _methodName, _ct);
                    EnsureRead(_read);
                    return _table;
                }

                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT 'Product' AS List, Id, Name, SortOrder FROM dbo.LeadProducts WHERE IsActive = 1 ");
                _sqlQuery.Append(" UNION ALL SELECT 'Source', Id, Name, SortOrder FROM dbo.LeadSources WHERE IsActive = 1 ");
                _sqlQuery.Append(" UNION ALL SELECT 'BillingCycle', Id, Name, SortOrder FROM dbo.LeadBillingCycles WHERE IsActive = 1 ");
                _sqlQuery.Append(" ORDER BY List, SortOrder, Name ");
                foreach (DataRow _row in (await Read(_sqlQuery.ToString())).Rows)
                {
                    var _value = new LeadListOption { Id = _row.GetInt("Id"), Name = _row.GetText("Name") };
                    switch (_row.GetText("List"))
                    {
                        case "Product": _options.Products.Add(_value); break;
                        case "Source": _options.Sources.Add(_value); break;
                        case "BillingCycle": _options.BillingCycles.Add(_value); break;
                    }
                }

                _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT 'Status' AS List, Id, Code, Name, SortOrder FROM dbo.LeadStatuses ");
                _sqlQuery.Append(" UNION ALL SELECT 'Type', Id, Code, Name, SortOrder FROM dbo.LeadTypes ");
                _sqlQuery.Append(" ORDER BY List, SortOrder ");
                foreach (DataRow _row in (await Read(_sqlQuery.ToString())).Rows)
                {
                    var _value = new LeadFixedOption { Id = _row.GetInt("Id"), Code = _row.GetText("Code"), Name = _row.GetText("Name") };
                    if (_row.GetText("List") == "Status") _options.Statuses.Add(_value); else _options.Types.Add(_value);
                }

                // Sales users (managers are sales users too); inactive ones only for the filters.
                _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT PublicId, CONCAT(FirstName, N' ', LastName) AS Name, Role, CAST(CASE WHEN Status = 'Active' THEN 1 ELSE 0 END AS BIT) AS IsActive ");
                _sqlQuery.Append(" FROM dbo.SalesUsers WHERE IsDeleted = 0 ");
                _sqlQuery.Append(" ORDER BY FirstName, LastName ");
                _options.SalesUsers = (await Read(_sqlQuery.ToString())).MapRows(_row => new SalesUserOption
                {
                    Id = _row.GetGuid("PublicId"), Name = _row.GetText("Name"), Role = _row.GetText("Role"), IsActive = _row.GetBool("IsActive")
                });
                return (_options, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(LeadDto lead, IFunctionReturn functionReturn)> SaveLeadAsync(Guid? _id, HttpLeadPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = _id.HasValue ? "F:Lead:Update" : "F:Lead:Create";
            try
            {
                _params ??= new HttpLeadPostParams();
                LeadDto _before = null;
                Guid _companyId;
                if (_id.HasValue)
                {
                    // Edit: only the creator or their manager; the company stays as it is.
                    _before = await ReadOneAsync(_id.Value, _user, _ct);
                    if (_before == null) return (null, NotFound(_methodName));
                    if (!_before.CanEdit) return (null, Forbidden("Only the person who created this lead and their manager can change it.", _methodName));
                    if (_params.CustomerId.HasValue && _params.CustomerId != _before.CompanyId)
                        return (null, Invalid(new List<string> { "The company of a lead cannot be changed." }, _methodName));
                    _companyId = _before.CompanyId;
                }
                else
                {
                    if (!_params.CustomerId.HasValue || _params.CustomerId == Guid.Empty)
                        return (null, Invalid(new List<string> { "Choose the company, or add a new one." }, _methodName));
                    _companyId = _params.CustomerId.Value;
                }

                (List<SqlParameter> _parameters, List<string> _errors) = Prepare(_params, _companyId);
                if (_errors.Count > 0) return (null, Invalid(_errors, _methodName));
                _parameters.Insert(0, P("@LeadPublicId", _id));
                _parameters.Insert(1, P("@ActorSalesUserId", _user.Id));

                Guid _savedId;
                try
                {
                    _savedId = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.LeadSave, _parameters, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                LeadDto _saved = await ReadOneAsync(_savedId, _user, _ct);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, _id.HasValue ? "UpdateLead" : "CreateLead", "Lead", _savedId, _user,
                    _id.HasValue ? new { Before = AuditOf(_before), After = AuditOf(_saved) } : AuditOf(_saved), _ipAddress, _ct);
                return (_saved, new FunctionReturn
                {
                    MethodName = _methodName,
                    HttpStatusCode = _id.HasValue ? Constants.HttpStatusCodes.Ok : Constants.HttpStatusCodes.Created,
                    Message = new List<string> { _id.HasValue ? $"Lead {_saved.LeadNumber} saved." : $"Lead {_saved.LeadNumber} added." }
                });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<IFunctionReturn> DeleteLeadAsync(Guid _id, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Lead:Delete";
            try
            {
                LeadDto _lead = await ReadOneAsync(_id, _user, _ct);
                if (_lead == null) return NotFound(_methodName);
                if (!_lead.CanDelete) return Forbidden("Only sales managers can delete leads: their own and their team's.", _methodName);
                try
                {
                    await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.LeadDelete, new[] { P("@LeadPublicId", _id), P("@ActorSalesUserId", _user.Id) }, _ct);
                }
                catch (AppException ex)
                {
                    return FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName);
                }
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "DeleteLead", "Lead", _id, _user, AuditOf(_lead), _ipAddress, _ct);
                return new FunctionReturn { MethodName = _methodName, Message = new List<string> { $"Lead {_lead.LeadNumber} deleted." } };
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemError(_methodName);
            }
        }

        public async Task<LeadDto> CreateForOrderAsync(Guid _saleId, Guid _companyId, OrderContactParams _contact, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            try
            {
                // An order saved with "No lead": an Open, Self Working lead for the same company and primary contact, generated by
                // and assigned to the person who saved the order, dated today. Its other fields are filled in later (Edit).
                var _params = new HttpLeadPostParams
                {
                    CustomerId = _companyId,
                    ClientName = _contact?.Name, ClientDesignation = _contact?.Designation, ClientEmail = _contact?.Email, ClientPhone = _contact?.Phone,
                    StatusId = LeadStatusOpen, TypeId = LeadTypeSelfWorking,
                    GeneratedById = _user.PublicId, AssignedId = _user.PublicId,
                    LeadDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330)),
                    Remarks = "Created automatically when the order was saved without a lead."
                };
                (LeadDto _lead, IFunctionReturn _functionReturn) = await SaveLeadAsync(null, _params, _user, _ipAddress, _ct);
                if (!_functionReturn.Status || _lead == null)
                {
                    _loggerFunctions.Error("F:Lead:CreateForOrder", "Lead not created: " + string.Join(" ", _functionReturn.Message ?? new List<string>()));
                    return null;
                }
                await LinkOrderAsync(_lead.Id, _saleId, _user, _ct);
                return await ReadOneAsync(_lead.Id, _user, _ct);
            }
            catch (Exception ex)
            {
                // The order is saved either way.
                _loggerFunctions.Error("F:Lead:CreateForOrder", ex.Message, ex);
                return null;
            }
        }

        public async Task LinkOrderAsync(Guid _leadId, Guid _saleId, SalesUserContext _user, CancellationToken _ct = default)
        {
            try
            {
                // Any lead with no order yet: the order form's lead picker offers every lead (everyone sees every lead),
                // and Lead Records > New Order offers Won leads.
                LeadDto _lead = await ReadOneAsync(_leadId, _user, _ct);
                if (_lead == null || _lead.SaleId.HasValue) return;
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" UPDATE l SET SaleId = s.Id ");
                _sqlQuery.Append(" FROM dbo.Leads l JOIN dbo.Sales s ON s.PublicId = @sale ");
                _sqlQuery.Append(" WHERE l.PublicId = @lead AND l.SaleId IS NULL AND l.IsDeleted = 0 ");
                await _databaseFunctions.ExecuteNonQueryAsync(_sqlQuery.ToString(), new[] { P("@lead", _leadId), P("@sale", _saleId) }, _ct);
            }
            catch (Exception ex)
            {
                // The order is saved either way; the link is only for the lead's row.
                _loggerFunctions.Error("F:Lead:LinkOrder", ex.Message, ex);
            }
        }
    }
}
