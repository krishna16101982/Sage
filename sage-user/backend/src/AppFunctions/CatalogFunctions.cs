using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.AppValidations;
using SageUserApi.Models;

namespace SageUserApi.AppFunctions
{
    /// <summary>What a sales user can sell: active categories, products and plans with price and cost (read-only).</summary>
    public class CatalogFunctions : ICatalogFunctions
    {
        private const string CatalogSelect = @"SELECT c.Id, c.Code, c.Name, p.Id, p.Code, p.Name, p.Description,
    pl.Id, pl.Name, pl.Description, pl.DurationUnit, pl.DurationCount, pl.Price, pl.Cost
FROM dbo.ProductCategories c
JOIN dbo.Products p ON p.CategoryId = c.Id AND p.IsActive = 1
JOIN dbo.ProductPlans pl ON pl.ProductId = p.Id AND pl.IsActive = 1
WHERE c.IsActive = 1
ORDER BY c.SortOrder, c.Name, p.SortOrder, p.Name, pl.SortOrder, pl.Name";

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        public CatalogFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
        }

        public async Task<(List<CatalogCategory> catalog, IFunctionReturn functionReturn)> GetCatalogAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:Catalog:GetCatalog";
            try
            {
                var _rows = await _databaseFunctions.QueryAsync(CatalogSelect, r => new
                {
                    CategoryId = r.GetInt64(0), CategoryCode = r.GetString(1), CategoryName = r.GetString(2),
                    ProductId = r.GetInt64(3), ProductCode = r.GetString(4), ProductName = r.GetString(5), ProductDescription = r.IsDBNull(6) ? null : r.GetString(6),
                    Plan = new CatalogPlan
                    {
                        Id = r.GetInt64(7), Name = r.GetString(8), Description = r.IsDBNull(9) ? null : r.GetString(9),
                        DurationUnit = r.GetString(10), DurationCount = r.GetInt32(11), Price = r.GetDecimal(12), Cost = r.GetDecimal(13)
                    }
                }, null, _ct);

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

                return (_catalog, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
