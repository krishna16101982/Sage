using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.Filter;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Sales for the Admin Panel (read-only; sales are recorded and cancelled in the User Panel): every sale, filterable by
    /// sales person or manager-wise (the manager's own sales plus their team's), and totals per person or per manager.
    /// </summary>
    public class SalesReportFunctions : ISalesReportFunctions
    {
        private static readonly string[] SortKeys = { "soldAt", "saleNumber", "customer", "product", "seller", "saleAmount", "renewalDate" };

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

        /// <summary>One sale row (from GetSaleAsync or dbo.usp_Sale_AdminGetPaged; both use these column names).</summary>
        private static AdminSaleDto MapSale(DataRow _row)
        {
            decimal _list = _row.GetDecimal("ListAmount"), _sale = _row.GetDecimal("SaleAmount");
            decimal _discount = _row.GetDecimal("DiscountAmount"), _cost = _row.GetDecimal("CostAmount");
            return new AdminSaleDto
            {
                Id = _row.GetGuid("PublicId"),
                SaleNumber = _row.GetText("SaleNumber"),
                CustomerId = _row.GetGuid("CustomerPublicId"),
                CustomerName = _row.GetText("CustomerName"),
                CategoryName = _row.GetText("CategoryName"),
                ProductName = _row.GetText("ProductName"),
                PlanName = _row.GetText("PlanName"),
                SellerId = _row.GetGuid("SellerPublicId"),
                SellerName = _row.GetText("SellerName"),
                SellerRole = _row.GetText("SellerRole"),
                ManagerName = _row.GetText("ManagerName"),
                Quantity = _row.GetInt("Quantity"),
                UnitPrice = _row.GetDecimal("UnitPrice"),
                UnitCost = _row.GetDecimal("UnitCost"),
                ListAmount = _list,
                SaleAmount = _sale,
                DiscountAmount = _discount,
                DiscountPercent = _list > 0 ? decimal.Round(_discount * 100m / _list, 2) : 0m,
                CostAmount = _cost,
                Margin = _sale - _cost,
                DurationUnit = _row.GetText("DurationUnit"),
                DurationCount = _row.GetInt("DurationCount"),
                StartDate = _row.GetDate("StartDate"),
                RenewalDate = _row.GetDate("RenewalDate"),
                Status = _row.GetText("Status"),
                Notes = _row.GetText("Notes"),
                CreatedAtUtc = _row.GetUtc("CreatedAtUtc"),
                CancelledAtUtc = _row.GetNullableUtc("CancelledAtUtc"),
                CancelledByName = _row.GetText("CancelledByName"),
                CancelReason = _row.GetText("CancelReason")
            };
        }

        /// <summary>One page of sales (or, with _exportAll, every matching sale up to MaxExportRows) plus the totals.</summary>
        private async Task<(List<AdminSaleDto> items, PaginationParams paging, int totalCount, decimal totalSale, decimal totalMargin)> QuerySalesAsync(
            AdminSaleFilter _filter, bool _exportAll, CancellationToken _ct)
        {
            _filter ??= new AdminSaleFilter();
            PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortKeys, "soldAt");
            string _status = _filter.Status == "Active" || _filter.Status == "Cancelled" ? _filter.Status : null;
            string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
            var _totalCount = IntOut("@TotalCount");
            var _totalSale = MoneyOut("@TotalSaleAmount");
            var _totalMargin = MoneyOut("@TotalMargin");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.SaleAdminGetPaged, new[]
            {
                P("@SellerPublicId", _filter.SellerId), P("@ManagerPublicId", _filter.ManagerId), P("@CustomerPublicId", _filter.CustomerId),
                P("@Search", _search), P("@Status", _status), Date("@SoldFrom", _filter.SoldFrom), Date("@SoldTo", _filter.SoldTo),
                P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection),
                P("@Offset", _exportAll ? 0 : _paging.Offset), P("@PageSize", _exportAll ? MaxExportRows + 1 : _paging.PageSize),
                _totalCount, _totalSale, _totalMargin
            }, "F:SalesReport:QuerySales", _ct);
            EnsureRead(_functionReturn);
            return (_dataTable.MapRows(MapSale), _paging, OutInt(_totalCount), OutMoney(_totalSale), OutMoney(_totalMargin));
        }

        private Task AuditExportAsync(long? _actorUserId, object _details, string _ipAddress, CancellationToken _ct)
            => _auditFunctions.LogAsync(_actorUserId, ApplicationEnumeration.AuditAction.Export.ToString(), ApplicationEnumeration.ObjectType.Sale.ToString(), null, null, _details, _ipAddress, _ct);

        #endregion

        public async Task<(AdminSalesPage result, IFunctionReturn functionReturn)> GetSalesAsync(AdminSaleFilter _filter, CancellationToken _ct = default)
        {
            string _methodName = "F:SalesReport:GetSales";
            try
            {
                (List<AdminSaleDto> _items, PaginationParams _paging, int _count, decimal _sum, decimal _margin) = await QuerySalesAsync(_filter, false, _ct);
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
                // Same column names as dbo.usp_Sale_AdminGetPaged, so MapSale reads both.
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT s.PublicId, s.SaleNumber, cu.PublicId AS CustomerPublicId, cu.Name AS CustomerName, c.Name AS CategoryName, ");
                _sqlQuery.Append("        p.Name AS ProductName, pl.Name AS PlanName, ");
                _sqlQuery.Append("        su.PublicId AS SellerPublicId, CONCAT(su.FirstName, N' ', su.LastName) AS SellerName, su.Role AS SellerRole, ");
                _sqlQuery.Append("        CASE WHEN mg.Id IS NULL THEN NULL ELSE CONCAT(mg.FirstName, N' ', mg.LastName) END AS ManagerName, ");
                _sqlQuery.Append("        s.Quantity, s.UnitPrice, s.UnitCost, s.ListAmount, s.SaleAmount, s.DiscountAmount, s.CostAmount, ");
                _sqlQuery.Append("        s.DurationUnit, s.DurationCount, s.StartDate, s.RenewalDate, s.Status, s.Notes, s.CreatedAtUtc, s.CancelledAtUtc, ");
                _sqlQuery.Append("        CASE WHEN cb.Id IS NULL THEN NULL ELSE CONCAT(cb.FirstName, N' ', cb.LastName) END AS CancelledByName, s.CancelReason ");
                _sqlQuery.Append(" FROM dbo.Sales s ");
                _sqlQuery.Append(" JOIN dbo.Customers cu ON cu.Id = s.CustomerId ");
                _sqlQuery.Append(" JOIN dbo.ProductCategories c ON c.Id = s.CategoryId ");
                _sqlQuery.Append(" JOIN dbo.Products p ON p.Id = s.ProductId ");
                _sqlQuery.Append(" JOIN dbo.ProductPlans pl ON pl.Id = s.PlanId ");
                _sqlQuery.Append(" JOIN dbo.SalesUsers su ON su.Id = s.SalesUserId ");
                _sqlQuery.Append(" LEFT JOIN dbo.SalesUsers mg ON mg.Id = s.ManagerId ");
                _sqlQuery.Append(" LEFT JOIN dbo.SalesUsers cb ON cb.Id = s.CancelledBySalesUserId ");
                _sqlQuery.Append(" WHERE s.PublicId = @id ");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                    _sqlQuery.ToString(), new[] { P("@id", _id) }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                DataRow _row = _dataTable.FirstRow();
                if (_row == null) return (null, ApplicationCommonFunctions.NotFound("Sale not found.", _methodName));
                return (MapSale(_row), _functionReturn);
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
                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.SaleSummary,
                    new[] { P("@GroupBy", _groupBy), Date("@SoldFrom", _filter?.SoldFrom), Date("@SoldTo", _filter?.SoldTo) }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                List<SalesSummaryRow> _rows = _dataTable.MapRows(_row => new SalesSummaryRow
                {
                    Id = _row.GetGuid("PublicId"),
                    Name = _row.GetText("Name"),
                    Username = _row.GetText("Username"),
                    Role = _row.GetText("Role"),
                    ManagerName = _row.GetText("ManagerName"),
                    TeamSize = _row.GetInt("TeamSize"),
                    SaleCount = _row.GetInt("SaleCount"),
                    CancelledCount = _row.GetInt("CancelledCount"),
                    Quantity = _row.GetInt("Quantity"),
                    ListAmount = _row.GetDecimal("ListAmount"),
                    DiscountAmount = _row.GetDecimal("DiscountAmount"),
                    SaleAmount = _row.GetDecimal("SaleAmount"),
                    CostAmount = _row.GetDecimal("CostAmount"),
                    Margin = _row.GetDecimal("Margin")
                });
                return (_rows, _functionReturn);
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
                // Everyone who can sell, plus deleted users who still have sales (their old sales stay filterable).
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT u.PublicId, CONCAT(u.FirstName, N' ', u.LastName) AS Name, u.Role, u.Status, ");
                _sqlQuery.Append("        CASE WHEN m.Id IS NULL THEN NULL ELSE CONCAT(m.FirstName, N' ', m.LastName) END AS ManagerName ");
                _sqlQuery.Append(" FROM dbo.SalesUsers u ");
                _sqlQuery.Append(" LEFT JOIN dbo.SalesUsers m ON m.Id = u.ManagerId ");
                _sqlQuery.Append(" WHERE u.IsDeleted = 0 OR EXISTS (SELECT 1 FROM dbo.Sales s WHERE s.SalesUserId = u.Id) ");
                _sqlQuery.Append(" ORDER BY u.FirstName, u.LastName ");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), null, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                List<SellerOption> _sellers = _dataTable.MapRows(_row => new SellerOption
                {
                    Id = _row.GetGuid("PublicId"),
                    Name = _row.GetText("Name"),
                    Role = _row.GetText("Role"),
                    Status = _row.GetText("Status"),
                    ManagerName = _row.GetText("ManagerName")
                });
                return (_sellers, _functionReturn);
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
                (List<AdminSaleDto> _items, _, int _count, decimal _sum, decimal _margin) = await QuerySalesAsync(_filter, true, _ct);
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
