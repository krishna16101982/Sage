using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Globals;

namespace SageUserApi.Controllers
{
    /// <summary>What can be sold: active categories > products > plans with price and cost.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/catalog")]
    [ApiController]
    public class CatalogController : SalesControllerBase
    {
        private readonly ICatalogFunctions _catalogFunctions;

        public CatalogController(ILoggerFunctions __loggerFunctions, IApiResponse __apiResponse, ISalesUserContextFunctions __userContext, ICatalogFunctions __catalogFunctions)
            : base(__loggerFunctions, __apiResponse, __userContext)
        {
            _catalogFunctions = __catalogFunctions;
        }

        /// <summary>Company rules for the sale page: maximum discount %, backdating limit, sale number prefix.</summary>
        [HttpGet("rules")]
        public Task<IActionResult> GetRules(CancellationToken _ct) => WithUserAsync("C:Catalog:GetRules", async _ =>
        {
            SageUserApi.Models.SalesRules _rules = await _catalogFunctions.GetSalesRulesAsync(_ct);
            return Result(_rules, new SageCommonLib.Models.FunctionReturn { MethodName = "C:Catalog:GetRules" });
        }, _ct);

        [HttpGet]
        public Task<IActionResult> GetCatalog(CancellationToken _ct) => WithUserAsync("C:Catalog:GetCatalog", async _ =>
        {
            var (_catalog, _fr) = await _catalogFunctions.GetCatalogAsync(_ct);
            return Result(_catalog, _fr);
        }, _ct);
    }
}
