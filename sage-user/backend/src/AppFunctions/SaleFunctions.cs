using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.AppValidations;
using SageUserApi.Globals;
using SageUserApi.Models;
using SageUserApi.Models.Filter;
using SageUserApi.Models.HttpPost;
using Msg = SageUserApi.Globals.ApplicationConstants.Messages;
using SP = SageUserApi.Globals.ApplicationDatabaseConstants.StoredProcedures;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// Recording, listing and cancelling sales. Visibility: a sales user sees their own sales; a sales manager also sees the
    /// sales of the people who reported to them when the sale was made. Totals are always recalculated by usp_Sale_Create.
    /// </summary>
    public class SaleFunctions : ISaleFunctions
    {
        private static readonly string[] SortKeys = { "soldAt", "saleNumber", "customer", "product", "saleAmount", "renewalDate" };

        /// <summary>Same columns, in the same order, as dbo.usp_Sale_GetPaged.</summary>
        private const string SaleSelect = @"SELECT s.PublicId, s.SaleNumber, cu.PublicId, cu.Name, c.Name, p.Name, pl.Name,
    su.PublicId, CONCAT(su.FirstName, N' ', su.LastName),
    s.Quantity, s.UnitPrice, s.UnitCost, s.ListAmount, s.SaleAmount, s.DiscountAmount, s.CostAmount,
    s.DurationUnit, s.DurationCount, s.StartDate, s.RenewalDate, s.Status, s.Notes, s.CreatedAtUtc,
    s.CancelledAtUtc, CASE WHEN cb.Id IS NULL THEN NULL ELSE CONCAT(cb.FirstName, N' ', cb.LastName) END, s.CancelReason,
    CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.SaleCancelRequests r WHERE r.SaleId = s.Id AND r.Status = 'Pending') THEN 1 ELSE 0 END AS BIT)
FROM dbo.Sales s
JOIN dbo.Customers cu ON cu.Id = s.CustomerId
JOIN dbo.ProductCategories c ON c.Id = s.CategoryId
JOIN dbo.Products p ON p.Id = s.ProductId
JOIN dbo.ProductPlans pl ON pl.Id = s.PlanId
JOIN dbo.SalesUsers su ON su.Id = s.SalesUserId
LEFT JOIN dbo.SalesUsers cb ON cb.Id = s.CancelledBySalesUserId
WHERE s.PublicId = @id AND (s.SalesUserId = @u OR s.ManagerId = @u)";

        private const string TeamSelect = @"SELECT u.PublicId, CONCAT(u.FirstName, N' ', u.LastName), u.Username, u.Status, u.CanRequestCancellation,
    (SELECT COUNT(*) FROM dbo.SaleCancelRequests r JOIN dbo.Sales s ON s.Id = r.SaleId
     WHERE r.RequestedBySalesUserId = u.Id AND r.Status = 'Pending' AND s.ManagerId = @u)
FROM dbo.SalesUsers u WHERE u.ManagerId = @u AND u.IsDeleted = 0 ORDER BY u.FirstName, u.LastName";

        /// <summary>Cancellation requests. The WHERE clause is added by the caller from fixed strings only.</summary>
        private const string RequestSelect = @"SELECT TOP 200 r.PublicId, s.PublicId, s.SaleNumber, cu.Name, p.Name, s.SaleAmount,
    rb.PublicId, CONCAT(rb.FirstName, N' ', rb.LastName), r.Reason, r.Status, r.RequestedAtUtc,
    CASE WHEN db.Id IS NULL THEN NULL ELSE CONCAT(db.FirstName, N' ', db.LastName) END, r.DecidedAtUtc, r.DecisionNote
FROM dbo.SaleCancelRequests r
JOIN dbo.Sales s ON s.Id = r.SaleId
JOIN dbo.Customers cu ON cu.Id = s.CustomerId
JOIN dbo.Products p ON p.Id = s.ProductId
JOIN dbo.SalesUsers rb ON rb.Id = r.RequestedBySalesUserId
LEFT JOIN dbo.SalesUsers db ON db.Id = r.DecidedBySalesUserId
WHERE (@status IS NULL OR r.Status = @status) AND ";

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly ICatalogFunctions _catalogFunctions;

        public SaleFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, ICatalogFunctions __catalogFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _catalogFunctions = __catalogFunctions;
        }

        #region Helpers

        private static SqlParameter P(string _name, object _value) => new SqlParameter(_name, _value ?? DBNull.Value);
        private static SqlParameter Date(string _name, DateOnly? _value)
            => new SqlParameter(_name, SqlDbType.Date) { Value = _value.HasValue ? _value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value };
        private static SqlParameter Money(string _name, decimal? _value)
            => new SqlParameter(_name, SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = _value.HasValue ? _value.Value : DBNull.Value };

        private static string S(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
        private static DateTime? Utc(SqlDataReader r, int i) => r.IsDBNull(i) ? null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);
        private static bool HasMoreThanTwoDecimals(decimal? _value) => _value.HasValue && decimal.Round(_value.Value, 2) != _value.Value;

        private static SaleDto MapSale(SqlDataReader r)
        {
            decimal _list = r.GetDecimal(12), _sale = r.GetDecimal(13), _discount = r.GetDecimal(14), _cost = r.GetDecimal(15);
            return new SaleDto
            {
                Id = r.GetGuid(0),
                SaleNumber = r.GetString(1),
                CustomerId = r.GetGuid(2),
                CustomerName = r.GetString(3),
                CategoryName = r.GetString(4),
                ProductName = r.GetString(5),
                PlanName = r.GetString(6),
                SellerId = r.GetGuid(7),
                SellerName = r.GetString(8),
                Quantity = r.GetInt32(9),
                UnitPrice = r.GetDecimal(10),
                UnitCost = r.GetDecimal(11),
                ListAmount = _list,
                SaleAmount = _sale,
                DiscountAmount = _discount,
                DiscountPercent = _list > 0 ? decimal.Round(_discount * 100m / _list, 2) : 0m,
                CostAmount = _cost,
                Margin = _sale - _cost,
                DurationUnit = r.GetString(16),
                DurationCount = r.GetInt32(17),
                StartDate = DateOnly.FromDateTime(r.GetDateTime(18)),
                RenewalDate = DateOnly.FromDateTime(r.GetDateTime(19)),
                Status = r.GetString(20),
                Notes = S(r, 21),
                CreatedAtUtc = Utc(r, 22).Value,
                CancelledAtUtc = Utc(r, 23),
                CancelledByName = S(r, 24),
                CancelReason = S(r, 25),
                HasPendingCancelRequest = r.GetBoolean(26)
            };
        }

        private Task<SaleDto> GetRowAsync(Guid _id, long _userId, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync(SaleSelect, MapSale, new[] { P("@id", _id), P("@u", _userId) }, _ct);

        private static IFunctionReturn Invalid(List<string> _errors, string _methodName)
            => new FunctionReturn { Status = false, MessageType = Constants.ReturnMessageTypes.ValidationError, Message = _errors, HttpStatusCode = Constants.HttpStatusCodes.BadRequest, MethodName = _methodName };

        private static IFunctionReturn ManagersOnly(string _methodName, string _message = Msg.ManagersOnly)
            => FunctionReturn.Fail(Constants.ReturnMessageTypes.PermissionDenied, _message, Constants.HttpStatusCodes.Forbidden, _methodName);

        private static CancelRequestDto MapRequest(SqlDataReader r) => new CancelRequestDto
        {
            Id = r.GetGuid(0), SaleId = r.GetGuid(1), SaleNumber = r.GetString(2), CustomerName = r.GetString(3), ProductName = r.GetString(4),
            SaleAmount = r.GetDecimal(5), RequestedById = r.GetGuid(6), RequestedByName = r.GetString(7), Reason = r.GetString(8), Status = r.GetString(9),
            RequestedAtUtc = Utc(r, 10).Value, DecidedByName = S(r, 11), DecidedAtUtc = Utc(r, 12), DecisionNote = S(r, 13)
        };

        /// <summary>Reason / note text: trimmed, tags removed, at most 500 characters.</summary>
        private static string CleanText(string _value) => string.IsNullOrWhiteSpace(_value) ? null : Sanitization.Sanitize(_value.Trim());

        #endregion

        public async Task<(SalesPage result, IFunctionReturn functionReturn)> GetSalesAsync(SaleFilter _filter, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:GetSales";
            try
            {
                _filter ??= new SaleFilter();
                bool _team = string.Equals(_filter.Scope, ApplicationDatabaseConstants.SaleScope.Team, StringComparison.OrdinalIgnoreCase);
                if (_team && !_user.IsManager) return (null, ManagersOnly(_methodName));

                PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortKeys, "soldAt");
                string _status = _filter.Status == ApplicationDatabaseConstants.SaleStatus.Active || _filter.Status == ApplicationDatabaseConstants.SaleStatus.Cancelled ? _filter.Status : null;
                string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
                var _totalCount = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                var _totalSale = new SqlParameter("@TotalSaleAmount", SqlDbType.Decimal) { Precision = 18, Scale = 2, Direction = ParameterDirection.Output };
                var _totalMargin = new SqlParameter("@TotalMargin", SqlDbType.Decimal) { Precision = 18, Scale = 2, Direction = ParameterDirection.Output };

                IReadOnlyList<SaleDto> _items = await _databaseFunctions.QueryProcedureAsync(SP.SaleGetPaged, MapSale, new List<SqlParameter>
                {
                    P("@SalesUserId", _user.Id), P("@Scope", _team ? ApplicationDatabaseConstants.SaleScope.Team : ApplicationDatabaseConstants.SaleScope.Mine),
                    P("@SellerPublicId", _team ? _filter.SellerId : null), P("@Search", _search), P("@Status", _status),
                    Date("@SoldFrom", _filter.SoldFrom), Date("@SoldTo", _filter.SoldTo),
                    P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection), P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize),
                    _totalCount, _totalSale, _totalMargin
                }, _ct);

                return (new SalesPage
                {
                    Items = _items,
                    Page = _paging.Page,
                    PageSize = _paging.PageSize,
                    TotalCount = _totalCount.Value is int _count ? _count : 0,
                    TotalSaleAmount = _totalSale.Value is decimal _sum ? _sum : 0m,
                    TotalMargin = _totalMargin.Value is decimal _margin ? _margin : 0m
                }, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SaleDto sale, IFunctionReturn functionReturn)> GetSaleAsync(Guid _id, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:GetSale";
            try
            {
                SaleDto _sale = await GetRowAsync(_id, _user.Id, _ct);
                if (_sale == null) return (null, ApplicationCommonFunctions.NotFound(Msg.SaleNotFound, _methodName));
                _sale.CancelRequests = (await _databaseFunctions.QueryAsync(RequestSelect + "s.PublicId = @sale ORDER BY r.RequestedAtUtc DESC", MapRequest,
                    new[] { P("@status", null), P("@sale", _id) }, _ct)).ToList();
                return (_sale, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SaleDto sale, IFunctionReturn functionReturn)> CreateSaleAsync(HttpSalePostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:CreateSale";
            try
            {
                _params ??= new HttpSalePostParams();
                var _errors = new List<string>();
                if (!_params.CustomerId.HasValue || _params.CustomerId == Guid.Empty) _errors.Add(Msg.CustomerRequired);
                if (_params.PlanId <= 0) _errors.Add(Msg.PlanRequired);
                DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (!_params.StartDate.HasValue) _errors.Add(Msg.StartDateRequired);
                else
                {
                    // Backdating limit from Admin Panel > Settings; the start date may be at most a year ahead.
                    int _maxBack = (await _catalogFunctions.GetSalesRulesAsync(_ct)).MaxBackdateDays;
                    if (_params.StartDate.Value < _today.AddDays(-_maxBack))
                        _errors.Add(_maxBack == 0 ? "The start date cannot be in the past (company setting)." : $"The start date cannot be more than {_maxBack} days in the past (company setting).");
                    else if (_params.StartDate.Value > _today.AddYears(1)) _errors.Add(Msg.StartDateTooFarAhead);
                }
                if (HasMoreThanTwoDecimals(_params.SaleAmount) || HasMoreThanTwoDecimals(_params.DiscountAmount)) _errors.Add(Msg.AmountDecimals);
                string _notes = string.IsNullOrWhiteSpace(_params.Notes) ? null : Sanitization.Sanitize(_params.Notes.Trim());
                if (_notes != null && _notes.Length > 1000) _errors.Add(Msg.NotesTooLong);
                if (_errors.Count > 0) return (null, Invalid(_errors, _methodName));

                // Quantity, amounts, duration and renewal date are checked by the procedure against the price list.
                Guid _id;
                try
                {
                    _id = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.SaleCreate, new[]
                    {
                        P("@SalesUserId", _user.Id), P("@CustomerPublicId", _params.CustomerId), P("@PlanId", _params.PlanId), P("@Quantity", _params.Quantity),
                        Money("@SaleAmount", _params.SaleAmount), Money("@DiscountAmount", _params.DiscountAmount),
                        P("@DurationUnit", _params.DurationUnit.NullIfWhiteSpace()), P("@DurationCount", _params.DurationCount),
                        Date("@StartDate", _params.StartDate), Date("@RenewalDate", _params.RenewalDate), P("@Notes", _notes)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                SaleDto _created = await GetRowAsync(_id, _user.Id, _ct);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "Create", "Sale", _id, _user, new
                {
                    _created.SaleNumber, _created.CustomerName, _created.ProductName, _created.PlanName, _created.Quantity,
                    _created.UnitPrice, _created.ListAmount, _created.SaleAmount, _created.DiscountAmount, _created.StartDate, _created.RenewalDate
                }, _ipAddress, _ct);
                return (_created, new FunctionReturn { MethodName = _methodName, HttpStatusCode = Constants.HttpStatusCodes.Created, Message = new List<string> { Msg.SaleCreated } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SaleDto sale, IFunctionReturn functionReturn)> CancelSaleAsync(Guid _id, HttpSaleCancelParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:CancelSale";
            try
            {
                // Sales persons ask their manager instead (CreateCancelRequestAsync).
                if (!_user.IsManager) return (null, ManagersOnly(_methodName, Msg.ManagersOnlyCancel));
                string _reason = CleanText(_params?.Reason);
                if (string.IsNullOrEmpty(_reason) || _reason.Length > 500)
                    return (null, ApplicationCommonFunctions.BadRequest(Msg.CancelReasonRequired, _methodName));

                // The procedure allows the sale's manager, or a manager for their own sale (404 for anyone else).
                (IFunctionReturn _functionReturn, long _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SaleCancel, new[]
                {
                    P("@SalePublicId", _id), P("@SalesUserId", _user.Id), P("@Reason", _reason)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                SaleDto _cancelled = await GetRowAsync(_id, _user.Id, _ct);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "Cancel", "Sale", _id, _user, new { _cancelled.SaleNumber, Reason = _reason }, _ipAddress, _ct);
                return (_cancelled, new FunctionReturn { MethodName = _methodName, Message = new List<string> { Msg.SaleCancelled } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(List<TeamMember> members, IFunctionReturn functionReturn)> GetTeamMembersAsync(SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:GetTeamMembers";
            try
            {
                if (!_user.IsManager) return (null, ManagersOnly(_methodName));
                var _members = await _databaseFunctions.QueryAsync(TeamSelect,
                    r => new TeamMember { Id = r.GetGuid(0), Name = r.GetString(1), Username = r.GetString(2), Status = r.GetString(3), CanRequestCancellation = r.GetBoolean(4), PendingRequests = r.GetInt32(5) },
                    new[] { P("@u", _user.Id) }, _ct);
                return (_members.ToList(), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(CancelRequestDto request, IFunctionReturn functionReturn)> CreateCancelRequestAsync(Guid _saleId, HttpSaleCancelParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:CreateCancelRequest";
            try
            {
                if (_user.IsManager) return (null, ApplicationCommonFunctions.BadRequest("Managers cancel sales directly.", _methodName));
                if (!_user.CanRequestCancellation) return (null, ManagersOnly(_methodName, Msg.RequestNotAllowed));
                string _reason = CleanText(_params?.Reason);
                if (string.IsNullOrEmpty(_reason) || _reason.Length > 500) return (null, ApplicationCommonFunctions.BadRequest(Msg.ReasonRequired, _methodName));

                Guid _requestId;
                try
                {
                    _requestId = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.SaleCancelRequestCreate, new[]
                    {
                        P("@SalePublicId", _saleId), P("@SalesUserId", _user.Id), P("@Reason", _reason)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                CancelRequestDto _request = await _databaseFunctions.QuerySingleOrDefaultAsync(RequestSelect + "r.PublicId = @id", MapRequest,
                    new[] { P("@status", null), P("@id", _requestId) }, _ct);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "CancelRequest", "Sale", _saleId, _user, new { _request.SaleNumber, Reason = _reason }, _ipAddress, _ct);
                return (_request, new FunctionReturn { MethodName = _methodName, HttpStatusCode = Constants.HttpStatusCodes.Created, Message = new List<string> { Msg.CancelRequested } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(List<CancelRequestDto> requests, IFunctionReturn functionReturn)> GetCancelRequestsAsync(string _status, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:GetCancelRequests";
            try
            {
                string _filter = _status == "Pending" || _status == "Approved" || _status == "Rejected" ? _status : null;
                // A manager sees the requests on their team's sales; a sales person sees their own requests.
                string _scope = _user.IsManager ? "s.ManagerId = @u" : "r.RequestedBySalesUserId = @u";
                var _requests = await _databaseFunctions.QueryAsync(RequestSelect + _scope + " ORDER BY CASE WHEN r.Status = 'Pending' THEN 0 ELSE 1 END, r.RequestedAtUtc DESC",
                    MapRequest, new[] { P("@status", _filter), P("@u", _user.Id) }, _ct);
                return (_requests.ToList(), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(CancelRequestDto request, IFunctionReturn functionReturn)> DecideCancelRequestAsync(Guid _requestId, bool _approve, HttpCancelDecisionParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:DecideCancelRequest";
            try
            {
                if (!_user.IsManager) return (null, ManagersOnly(_methodName, Msg.ManagersOnlyTeam));
                string _note = CleanText(_params?.Note);
                if (_note != null && _note.Length > 500) return (null, ApplicationCommonFunctions.BadRequest(Msg.NoteTooLong, _methodName));

                Guid _saleId;
                try
                {
                    _saleId = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.SaleCancelRequestDecide, new[]
                    {
                        P("@RequestPublicId", _requestId), P("@ManagerId", _user.Id), P("@Approve", _approve), P("@Note", _note)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                CancelRequestDto _request = await _databaseFunctions.QuerySingleOrDefaultAsync(RequestSelect + "r.PublicId = @id", MapRequest,
                    new[] { P("@status", null), P("@id", _requestId) }, _ct);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, _approve ? "CancelRequestApprove" : "CancelRequestReject", "Sale", _saleId, _user,
                    new { _request.SaleNumber, _request.RequestedByName, _request.Reason, Note = _note }, _ipAddress, _ct);
                return (_request, new FunctionReturn { MethodName = _methodName, Message = new List<string> { _approve ? Msg.RequestApproved : Msg.RequestRejected } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(TeamMember member, IFunctionReturn functionReturn)> SetCancelPermissionAsync(Guid _memberId, HttpCancelPermissionParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:SetCancelPermission";
            try
            {
                if (!_user.IsManager) return (null, ManagersOnly(_methodName, Msg.ManagersOnlyTeam));
                bool _allowed = _params?.CanRequestCancellation ?? false;
                (IFunctionReturn _functionReturn, long _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SalesUserSetCancelPermission, new[]
                {
                    P("@ManagerId", _user.Id), P("@MemberPublicId", _memberId), P("@CanRequestCancellation", _allowed)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                TeamMember _member = (await _databaseFunctions.QueryAsync(TeamSelect,
                    r => new TeamMember { Id = r.GetGuid(0), Name = r.GetString(1), Username = r.GetString(2), Status = r.GetString(3), CanRequestCancellation = r.GetBoolean(4), PendingRequests = r.GetInt32(5) },
                    new[] { P("@u", _user.Id) }, _ct)).FirstOrDefault(m => m.Id == _memberId);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "CancelPermission", "SalesUser", _memberId, _user, new { CanRequestCancellation = _allowed }, _ipAddress, _ct);
                return (_member, new FunctionReturn { MethodName = _methodName, Message = new List<string> { Msg.PermissionSaved } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
