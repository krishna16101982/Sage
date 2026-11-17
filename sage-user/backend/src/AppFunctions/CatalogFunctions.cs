using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.AppValidations;
using SageUserApi.Models;
using static SageCommonLib.AppFunctions.Sql;

namespace SageUserApi.AppFunctions
{
    /// <summary>What a sales user can sell: active categories, products and plans with price and cost (read-only).</summary>
    public class CatalogFunctions : ICatalogFunctions
    {
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        public CatalogFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
        }

        /// <summary>Reads the sales settings; a missing or unreadable value keeps its default.</summary>
        public async Task<SalesRules> GetSalesRulesAsync(CancellationToken _ct = default)
        {
            var _rules = new SalesRules();

            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT SettingKey, SettingValue FROM dbo.Settings ");
            _sqlQuery.Append(" WHERE SettingKey IN ('Sales.MaxDiscountPercent', 'Sales.MaxBackdateDays', 'Sales.SaleNumberPrefix') ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), null, "F:Catalog:GetSalesRules", _ct);
            EnsureRead(_functionReturn);
            foreach (DataRow _row in _dataTable.Rows)
            {
                string _key = _row.GetText("SettingKey");
                string _value = _row.GetText("SettingValue");
                if (_key == "Sales.MaxDiscountPercent" && decimal.TryParse(_value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal _d) && _d >= 0 && _d <= 100) _rules.MaxDiscountPercent = _d;
                else if (_key == "Sales.MaxBackdateDays" && int.TryParse(_value, out int _days) && _days >= 0) _rules.MaxBackdateDays = _days;
                else if (_key == "Sales.SaleNumberPrefix" && !string.IsNullOrWhiteSpace(_value)) _rules.SaleNumberPrefix = _value.Trim();
            }
            return _rules;
        }

        public async Task<(List<CatalogCategory> catalog, IFunctionReturn functionReturn)> GetCatalogAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:Catalog:GetCatalog";
            try
            {
                // Active categories > active products > active plans (a product with no active plan is left out).
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT c.Id AS CategoryId, c.Code AS CategoryCode, c.Name AS CategoryName, ");
                _sqlQuery.Append("        p.Id AS ProductId, p.Code AS ProductCode, p.Name AS ProductName, p.Description AS ProductDescription, ");
                _sqlQuery.Append("        pl.Id AS PlanId, pl.Name AS PlanName, pl.Description AS PlanDescription, ");
                _sqlQuery.Append("        pl.IncludedItemTitle, pl.IncludedItemDescription, ");
                _sqlQuery.Append("        pl.DurationUnit, pl.DurationCount, pl.Price, pl.Cost ");
                _sqlQuery.Append(" FROM dbo.ProductCategories c ");
                _sqlQuery.Append(" JOIN dbo.Products p ON p.CategoryId = c.Id AND p.IsActive = 1 ");
                _sqlQuery.Append(" JOIN dbo.ProductPlans pl ON pl.ProductId = p.Id AND pl.IsActive = 1 ");
                _sqlQuery.Append(" WHERE c.IsActive = 1 ");
                _sqlQuery.Append(" ORDER BY c.SortOrder, c.Name, p.SortOrder, p.Name, pl.SortOrder, pl.Name ");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), null, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                var _rows = _dataTable.MapRows(_row => new
                {
                    CategoryId = _row.GetLong("CategoryId"),
                    CategoryCode = _row.GetText("CategoryCode"),
                    CategoryName = _row.GetText("CategoryName"),
                    ProductId = _row.GetLong("ProductId"),
                    ProductCode = _row.GetText("ProductCode"),
                    ProductName = _row.GetText("ProductName"),
                    ProductDescription = _row.GetText("ProductDescription"),
                    Plan = new CatalogPlan
                    {
                        Id = _row.GetLong("PlanId"),
                        Name = _row.GetText("PlanName"),
                        Description = _row.GetText("PlanDescription"),
                        IncludedItemTitle = _row.GetText("IncludedItemTitle"),
                        IncludedItemDescription = _row.GetText("IncludedItemDescription"),
                        DurationUnit = _row.GetText("DurationUnit"),
                        DurationCount = _row.GetInt("DurationCount"),
                        Price = _row.GetDecimal("Price"),
                        Cost = _row.GetDecimal("Cost")
                    }
                });

                // Rows arrive sorted; group them while keeping that order.
                var _catalog = _rows.GroupBy(x => x.CategoryId).Select(c => new CatalogCategory
                {
                    Id = c.Key,
                    Code = c.First().CategoryCode,
                    Name = c.First().CategoryName,
                    Products = c.GroupBy(x => x.ProductId).Select(p => new CatalogProduct
                    {
                        Id = p.Key,
                        Code = p.First().ProductCode,
                        Name = p.First().ProductName,
                        Description = p.First().ProductDescription,
                        Plans = p.Select(x => x.Plan).ToList()
                    }).ToList()
                }).ToList();

                await AddSeatPricesAsync(_rows.Select(x => x.Plan).ToList(), _ct);
                return (_catalog, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        /// <summary>
        /// Seat prices (DDL 032) for the financial year of today in India, the same year dbo.usp_SaleOrder_Create uses.
        /// A plan that has seat prices but none for this year gets HasSeatPrices = true and an empty list (it cannot be sold
        /// on the order form until the admin adds them).
        /// </summary>
        private async Task AddSeatPricesAsync(List<CatalogPlan> _plans, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" DECLARE @today DATE = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+05:30') AS DATE); ");
            _sqlQuery.Append(" DECLARE @fy INT = CASE WHEN MONTH(@today) >= 4 THEN YEAR(@today) + 1 ELSE YEAR(@today) END; ");
            _sqlQuery.Append(" SELECT @fy AS FiscalYear, pl.Id AS PlanId, sp.MinSeats, sp.MaxSeats, sp.Price, ISNULL(sp.Cost, pl.Cost) AS Cost ");
            _sqlQuery.Append(" FROM dbo.ProductPlans pl ");
            _sqlQuery.Append(" LEFT JOIN dbo.ProductPlanSeatPrices sp ON sp.PlanId = pl.Id AND sp.FiscalYear = @fy ");
            _sqlQuery.Append(" WHERE pl.IsActive = 1 AND EXISTS (SELECT 1 FROM dbo.ProductPlanSeatPrices x WHERE x.PlanId = pl.Id) ");
            _sqlQuery.Append(" ORDER BY pl.Id, sp.MinSeats ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), null, "F:Catalog:SeatPrices", _ct);
            EnsureRead(_functionReturn);

            Dictionary<long, CatalogPlan> _byId = _plans.ToDictionary(p => p.Id);
            foreach (DataRow _row in _dataTable.Rows)
            {
                if (!_byId.TryGetValue(_row.GetLong("PlanId"), out CatalogPlan _plan)) continue;
                _plan.HasSeatPrices = true;
                _plan.PriceFiscalYear = _row.GetInt("FiscalYear");
                int? _min = _row.GetNullableInt("MinSeats");
                if (_min.HasValue)
                    _plan.SeatPrices.Add(new CatalogSeatPrice { MinSeats = _min.Value, MaxSeats = _row.GetNullableInt("MaxSeats"), Price = _row.GetDecimal("Price"), Cost = _row.GetDecimal("Cost") });
            }
        }
    }
}
