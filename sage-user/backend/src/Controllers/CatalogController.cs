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

        [HttpGet]
        public Task<IActionResult> GetCatalog(CancellationToken _ct) => WithUserAsync("C:Catalog:GetCatalog", async _ =>
        {
            var (_catalog, _fr) = await _catalogFunctions.GetCatalogAsync(_ct);
            return Result(_catalog, _fr);
        }, _ct);
    }
}
