using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
    /// Sales for the Admin Panel (read-only; sales are recorded and cancelled in the User Panel): every sale, filterable by
    /// sales person or manager-wise (the manager's own sales plus their team's), and totals per person or per manager.
    /// </summary>
    public class SalesReportFunctions : ISalesReportFunctions
    {
        private static readonly string[] SortKeys = { "soldAt", "saleNumber", "customer", "product", "seller", "saleAmount", "renewalDate" };

        /// <summary>Same columns, in the same order, as dbo.usp_Sale_AdminGetPaged.</summary>
        private const string SaleSelect = @"SELECT s.PublicId, s.SaleNumber, cu.PublicId, cu.Name, c.Name, p.Name, pl.Name,
    su.PublicId, CONCAT(su.FirstName, N' ', su.LastName), su.Role,
    CASE WHEN mg.Id IS NULL THEN NULL ELSE CONCAT(mg.FirstName, N' ', mg.LastName) END,
    s.Quantity, s.UnitPrice, s.UnitCost, s.ListAmount, s.SaleAmount, s.DiscountAmount, s.CostAmount,
    s.DurationUnit, s.DurationCount, s.StartDate, s.RenewalDate, s.Status, s.Notes, s.CreatedAtUtc,
    s.CancelledAtUtc, CASE WHEN cb.Id IS NULL THEN NULL ELSE CONCAT(cb.FirstName, N' ', cb.LastName) END, s.CancelReason
FROM dbo.Sales s
JOIN dbo.Customers cu ON cu.Id = s.CustomerId
JOIN dbo.ProductCategories c ON c.Id = s.CategoryId
JOIN dbo.Products p ON p.Id = s.ProductId
JOIN dbo.ProductPlans pl ON pl.Id = s.PlanId
JOIN dbo.SalesUsers su ON su.Id = s.SalesUserId
LEFT JOIN dbo.SalesUsers mg ON mg.Id = s.ManagerId
LEFT JOIN dbo.SalesUsers cb ON cb.Id = s.CancelledBySalesUserId
WHERE s.PublicId = @id";

        private const string SellersSelect = @"SELECT u.PublicId, CONCAT(u.FirstName, N' ', u.LastName), u.Role, u.Status,
    CASE WHEN m.Id IS NULL THEN NULL ELSE CONCAT(m.FirstName, N' ', m.LastName) END
FROM dbo.SalesUsers u LEFT JOIN dbo.SalesUsers m ON m.Id = u.ManagerId
WHERE u.IsDeleted = 0 OR EXISTS (SELECT 1 FROM dbo.Sales s WHERE s.SalesUserId = u.Id)
ORDER BY u.FirstName, u.LastName";

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        public SalesReportFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
        }

        #region Helpers

        private static SqlParameter P(string _name, object _value) => new SqlParameter(_name, _value ?? DBNull.Value);
        private static SqlParameter Date(string _name, DateOnly? _value)
            => new SqlParameter(_name, SqlDbType.Date) { Value = _value.HasValue ? _value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value };
        private static SqlParameter MoneyOut(string _name) => new SqlParameter(_name, SqlDbType.Decimal) { Precision = 18, Scale = 2, Direction = ParameterDirection.Output };
        private static string S(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
        private static DateTime? Utc(SqlDataReader r, int i) => r.IsDBNull(i) ? null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);

        private static AdminSaleDto MapSale(SqlDataReader r)
        {
            decimal _list = r.GetDecimal(14), _sale = r.GetDecimal(15), _discount = r.GetDecimal(16), _cost = r.GetDecimal(17);
            return new AdminSaleDto
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
                SellerRole = r.GetString(9),
                ManagerName = S(r, 10),
                Quantity = r.GetInt32(11),
                UnitPrice = r.GetDecimal(12),
                UnitCost = r.GetDecimal(13),
                ListAmount = _list,
                SaleAmount = _sale,
                DiscountAmount = _discount,
                DiscountPercent = _list > 0 ? decimal.Round(_discount * 100m / _list, 2) : 0m,
                CostAmount = _cost,
                Margin = _sale - _cost,
                DurationUnit = r.GetString(18),
                DurationCount = r.GetInt32(19),
                StartDate = DateOnly.FromDateTime(r.GetDateTime(20)),
                RenewalDate = DateOnly.FromDateTime(r.GetDateTime(21)),
                Status = r.GetString(22),
                Notes = S(r, 23),
                CreatedAtUtc = Utc(r, 24).Value,
                CancelledAtUtc = Utc(r, 25),
                CancelledByName = S(r, 26),
                CancelReason = S(r, 27)
            };
        }

        #endregion

        public async Task<(AdminSalesPage result, IFunctionReturn functionReturn)> GetSalesAsync(AdminSaleFilter _filter, CancellationToken _ct = default)
        {
            string _methodName = "F:SalesReport:GetSales";
            try
            {
                _filter ??= new AdminSaleFilter();
                PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortKeys, "soldAt");
                string _status = _filter.Status == "Active" || _filter.Status == "Cancelled" ? _filter.Status : null;
                string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
                var _totalCount = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                var _totalSale = MoneyOut("@TotalSaleAmount");
                var _totalMargin = MoneyOut("@TotalMargin");

                IReadOnlyList<AdminSaleDto> _items = await _databaseFunctions.QueryProcedureAsync(SP.SaleAdminGetPaged, MapSale, new List<SqlParameter>
                {
                    P("@SellerPublicId", _filter.SellerId), P("@ManagerPublicId", _filter.ManagerId), P("@CustomerPublicId", _filter.CustomerId),
                    P("@Search", _search), P("@Status", _status), Date("@SoldFrom", _filter.SoldFrom), Date("@SoldTo", _filter.SoldTo),
                    P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection), P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize),
                    _totalCount, _totalSale, _totalMargin
                }, _ct);

                return (new AdminSalesPage
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

        public async Task<(AdminSaleDto sale, IFunctionReturn functionReturn)> GetSaleAsync(Guid _id, CancellationToken _ct = default)
        {
            string _methodName = "F:SalesReport:GetSale";
            try
            {
                AdminSaleDto _sale = await _databaseFunctions.QuerySingleOrDefaultAsync(SaleSelect, MapSale, new[] { P("@id", _id) }, _ct);
                return _sale == null ? (null, ApplicationCommonFunctions.NotFound("Sale not found.", _methodName)) : (_sale, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(List<SalesSummaryRow> rows, IFunctionReturn functionReturn)> GetSummaryAsync(SalesSummaryFilter _filter, CancellationToken _ct = default)
        {
            string _methodName = "F:SalesReport:GetSummary";
            try
            {
                string _groupBy = string.Equals(_filter?.GroupBy, "manager", StringComparison.OrdinalIgnoreCase) ? "manager" : "seller";
                var _rows = await _databaseFunctions.QueryProcedureAsync(SP.SaleSummary, r => new SalesSummaryRow
                {
                    Id = r.GetGuid(0),
                    Name = r.GetString(1),
                    Username = r.GetString(2),
                    Role = r.GetString(3),
                    ManagerName = S(r, 4),
                    TeamSize = r.GetInt32(5),
                    SaleCount = r.GetInt32(6),
                    CancelledCount = r.GetInt32(7),
                    Quantity = r.GetInt32(8),
                    ListAmount = r.GetDecimal(9),
                    DiscountAmount = r.GetDecimal(10),
                    SaleAmount = r.GetDecimal(11),
                    CostAmount = r.GetDecimal(12),
                    Margin = r.GetDecimal(13)
                }, new[] { P("@GroupBy", _groupBy), Date("@SoldFrom", _filter?.SoldFrom), Date("@SoldTo", _filter?.SoldTo) }, _ct);
                return (_rows.ToList(), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(List<SellerOption> sellers, IFunctionReturn functionReturn)> GetSellersAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:SalesReport:GetSellers";
            try
            {
                var _sellers = await _databaseFunctions.QueryAsync(SellersSelect,
                    r => new SellerOption { Id = r.GetGuid(0), Name = r.GetString(1), Role = r.GetString(2), Status = r.GetString(3), ManagerName = S(r, 4) }, null, _ct);
                return (_sellers.ToList(), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
