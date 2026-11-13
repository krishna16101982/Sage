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
    s.CancelledAtUtc, CASE WHEN cb.Id IS NULL THEN NULL ELSE CONCAT(cb.FirstName, N' ', cb.LastName) END, s.CancelReason
FROM dbo.Sales s
JOIN dbo.Customers cu ON cu.Id = s.CustomerId
JOIN dbo.ProductCategories c ON c.Id = s.CategoryId
JOIN dbo.Products p ON p.Id = s.ProductId
JOIN dbo.ProductPlans pl ON pl.Id = s.PlanId
JOIN dbo.SalesUsers su ON su.Id = s.SalesUserId
LEFT JOIN dbo.SalesUsers cb ON cb.Id = s.CancelledBySalesUserId
WHERE s.PublicId = @id AND (s.SalesUserId = @u OR s.ManagerId = @u)";

        private const string TeamSelect = @"SELECT PublicId, CONCAT(FirstName, N' ', LastName), Username, Status
FROM dbo.SalesUsers WHERE ManagerId = @u AND IsDeleted = 0 ORDER BY FirstName, LastName";

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        public SaleFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
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
                CancelReason = S(r, 25)
            };
        }

        private Task<SaleDto> GetRowAsync(Guid _id, long _userId, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync(SaleSelect, MapSale, new[] { P("@id", _id), P("@u", _userId) }, _ct);

        private static IFunctionReturn Invalid(List<string> _errors, string _methodName)
            => new FunctionReturn { Status = false, MessageType = Constants.ReturnMessageTypes.ValidationError, Message = _errors, HttpStatusCode = Constants.HttpStatusCodes.BadRequest, MethodName = _methodName };

        private static IFunctionReturn ManagersOnly(string _methodName)
            => FunctionReturn.Fail(Constants.ReturnMessageTypes.PermissionDenied, Msg.ManagersOnly, Constants.HttpStatusCodes.Forbidden, _methodName);

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
                return _sale == null
                    ? (null, ApplicationCommonFunctions.NotFound(Msg.SaleNotFound, _methodName))
                    : (_sale, new FunctionReturn { MethodName = _methodName });
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
                else if (_params.StartDate.Value < _today.AddYears(-1) || _params.StartDate.Value > _today.AddYears(1)) _errors.Add(Msg.StartDateOutOfRange);
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
                string _reason = string.IsNullOrWhiteSpace(_params?.Reason) ? null : Sanitization.Sanitize(_params.Reason.Trim());
                if (string.IsNullOrEmpty(_reason) || _reason.Length > 500)
                    return (null, ApplicationCommonFunctions.BadRequest(Msg.CancelReasonRequired, _methodName));

                // The procedure allows the seller and the seller's manager only (404 for anyone else).
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
                    r => new TeamMember { Id = r.GetGuid(0), Name = r.GetString(1), Username = r.GetString(2), Status = r.GetString(3) },
                    new[] { P("@u", _user.Id) }, _ct);
                return (_members.ToList(), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
