using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
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
        private readonly IAuditFunctions _auditFunctions;

        /// <summary>An export is refused above this many sales (the admin narrows the dates or filters instead).</summary>
        public const int MaxExportRows = 50000;

        public SalesReportFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IAuditFunctions __auditFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _auditFunctions = __auditFunctions;
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

        /// <summary>One page of sales (or, with _exportAll, every matching sale up to MaxExportRows) plus the totals.</summary>
        private async Task<(IReadOnlyList<AdminSaleDto> items, PaginationParams paging, int totalCount, decimal totalSale, decimal totalMargin)> QuerySalesAsync(
            AdminSaleFilter _filter, bool _exportAll, CancellationToken _ct)
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
                P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection),
                P("@Offset", _exportAll ? 0 : _paging.Offset), P("@PageSize", _exportAll ? MaxExportRows + 1 : _paging.PageSize),
                _totalCount, _totalSale, _totalMargin
            }, _ct);
            return (_items, _paging, _totalCount.Value is int _count ? _count : 0, _totalSale.Value is decimal _sum ? _sum : 0m, _totalMargin.Value is decimal _margin ? _margin : 0m);
        }

        private Task AuditExportAsync(long? _actorUserId, object _details, string _ipAddress, CancellationToken _ct)
            => _auditFunctions.LogAsync(_actorUserId, ApplicationEnumeration.AuditAction.Export.ToString(), ApplicationEnumeration.ObjectType.Sale.ToString(), null, null, _details, _ipAddress, _ct);

        #endregion

        public async Task<(AdminSalesPage result, IFunctionReturn functionReturn)> GetSalesAsync(AdminSaleFilter _filter, CancellationToken _ct = default)
        {
            string _methodName = "F:SalesReport:GetSales";
            try
            {
                (IReadOnlyList<AdminSaleDto> _items, PaginationParams _paging, int _count, decimal _sum, decimal _margin) = await QuerySalesAsync(_filter, false, _ct);
                return (new AdminSalesPage
                {
                    Items = _items,
                    Page = _paging.Page,
                    PageSize = _paging.PageSize,
                    TotalCount = _count,
                    TotalSaleAmount = _sum,
                    TotalMargin = _margin
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

        /// <summary>Every sale matching the same filters as the Sales list, as a CSV file (refused above MaxExportRows). Audited.</summary>
        public async Task<(ExportFile file, IFunctionReturn functionReturn)> ExportSalesAsync(AdminSaleFilter _filter, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:SalesReport:ExportSales";
            try
            {
                _filter ??= new AdminSaleFilter();
                (IReadOnlyList<AdminSaleDto> _items, _, int _count, decimal _sum, decimal _margin) = await QuerySalesAsync(_filter, true, _ct);
                if (_count > MaxExportRows)
                    return (null, ApplicationCommonFunctions.BadRequest(
                        $"{_count:N0} sales match. An export can hold up to {MaxExportRows:N0}: choose a shorter date range or more filters, then export again.", _methodName));

                var _csv = new CsvBuilder("Sale number", "Sold at (UTC)", "Customer", "Category", "Product", "Plan", "Sales person", "Role", "Manager",
                    "Quantity", "Unit price", "List amount", "Discount", "Discount %", "Sale amount", "Cost", "Margin", "Duration",
                    "Start date", "Renewal date", "Status", "Cancelled at (UTC)", "Cancelled by", "Cancel reason", "Notes");
                foreach (AdminSaleDto x in _items)
                {
                    _csv.Row(CsvBuilder.Text(x.SaleNumber), CsvBuilder.Date(x.CreatedAtUtc), CsvBuilder.Text(x.CustomerName), CsvBuilder.Text(x.CategoryName),
                        CsvBuilder.Text(x.ProductName), CsvBuilder.Text(x.PlanName), CsvBuilder.Text(x.SellerName), CsvBuilder.Text(x.SellerRole),
                        CsvBuilder.Text(x.ManagerName), CsvBuilder.Number(x.Quantity), CsvBuilder.Number(x.UnitPrice), CsvBuilder.Number(x.ListAmount),
                        CsvBuilder.Number(x.DiscountAmount), CsvBuilder.Number(x.DiscountPercent), CsvBuilder.Number(x.SaleAmount), CsvBuilder.Number(x.CostAmount),
                        CsvBuilder.Number(x.Margin), CsvBuilder.Text($"{x.DurationCount} {x.DurationUnit}"), CsvBuilder.Date(x.StartDate), CsvBuilder.Date(x.RenewalDate),
                        CsvBuilder.Text(x.Status), CsvBuilder.Date(x.CancelledAtUtc), CsvBuilder.Text(x.CancelledByName), CsvBuilder.Text(x.CancelReason),
                        CsvBuilder.Text(x.Notes));
                }

                await AuditExportAsync(_actorUserId, new
                {
                    Report = "Sales", _filter.SellerId, _filter.ManagerId, _filter.CustomerId, _filter.Search, _filter.Status, _filter.SoldFrom, _filter.SoldTo,
                    Rows = _csv.RowCount, TotalSaleAmount = _sum, TotalMargin = _margin
                }, _ipAddress, _ct);
                return (new ExportFile { FileName = FileName("sales", _filter.SoldFrom, _filter.SoldTo), Content = _csv.ToBytes(), RowCount = _csv.RowCount },
                    new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        /// <summary>The summary per sales person or per manager (own + team) as a CSV file, with a total row. Audited.</summary>
        public async Task<(ExportFile file, IFunctionReturn functionReturn)> ExportSummaryAsync(SalesSummaryFilter _filter, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:SalesReport:ExportSummary";
            try
            {
                (List<SalesSummaryRow> _rows, IFunctionReturn _functionReturn) = await GetSummaryAsync(_filter, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                bool _byManager = string.Equals(_filter?.GroupBy, "manager", StringComparison.OrdinalIgnoreCase);

                var _csv = _byManager
                    ? new CsvBuilder("Manager", "Username", "Team size", "Sales", "Cancelled", "Quantity", "List amount", "Discount", "Sale amount", "Cost", "Margin")
                    : new CsvBuilder("Sales person", "Username", "Role", "Manager", "Sales", "Cancelled", "Quantity", "List amount", "Discount", "Sale amount", "Cost", "Margin");
                static string[] Totals(SalesSummaryRow r) => new[]
                {
                    CsvBuilder.Number(r.SaleCount), CsvBuilder.Number(r.CancelledCount), CsvBuilder.Number(r.Quantity), CsvBuilder.Number(r.ListAmount),
                    CsvBuilder.Number(r.DiscountAmount), CsvBuilder.Number(r.SaleAmount), CsvBuilder.Number(r.CostAmount), CsvBuilder.Number(r.Margin)
                };
                foreach (SalesSummaryRow r in _rows)
                {
                    string[] _who = _byManager
                        ? new[] { CsvBuilder.Text(r.Name), CsvBuilder.Text(r.Username), CsvBuilder.Number(r.TeamSize) }
                        : new[] { CsvBuilder.Text(r.Name), CsvBuilder.Text(r.Username), CsvBuilder.Text(r.Role), CsvBuilder.Text(r.ManagerName) };
                    _csv.Row(_who.Concat(Totals(r)).ToArray());
                }
                var _total = new SalesSummaryRow
                {
                    SaleCount = _rows.Sum(r => r.SaleCount), CancelledCount = _rows.Sum(r => r.CancelledCount), Quantity = _rows.Sum(r => r.Quantity),
                    ListAmount = _rows.Sum(r => r.ListAmount), DiscountAmount = _rows.Sum(r => r.DiscountAmount), SaleAmount = _rows.Sum(r => r.SaleAmount),
                    CostAmount = _rows.Sum(r => r.CostAmount), Margin = _rows.Sum(r => r.Margin)
                };
                _csv.Row((_byManager ? new[] { "Total", "", "" } : new[] { "Total", "", "", "" }).Concat(Totals(_total)).ToArray());

                await AuditExportAsync(_actorUserId, new { Report = _byManager ? "SummaryByManager" : "SummaryBySeller", _filter?.SoldFrom, _filter?.SoldTo, Rows = _rows.Count },
                    _ipAddress, _ct);
                return (new ExportFile
                {
                    FileName = FileName(_byManager ? "sales-by-manager" : "sales-by-person", _filter?.SoldFrom, _filter?.SoldTo),
                    Content = _csv.ToBytes(),
                    RowCount = _rows.Count
                }, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        /// <summary>"sales_2026-11-01_to_2026-11-30.csv", or with today's date when no range is chosen.</summary>
        private static string FileName(string _name, DateOnly? _from, DateOnly? _to)
        {
            if (_from.HasValue || _to.HasValue)
                return $"{_name}_{(_from.HasValue ? CsvBuilder.Date(_from.Value) : "start")}_to_{(_to.HasValue ? CsvBuilder.Date(_to.Value) : "today")}.csv";
            return $"{_name}_{CsvBuilder.Date(DateOnly.FromDateTime(DateTime.UtcNow))}.csv";
        }
    }
}
