using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Globals;
using SageUserApi.Models.Filter;
using SageUserApi.Models.HttpPost;

namespace SageUserApi.Controllers
{
    /// <summary>Record sales, list my / my team's sales, cancel a sale.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/sales")]
    [ApiController]
    public class SalesController : SalesControllerBase
    {
        private readonly ISaleFunctions _saleFunctions;

        public SalesController(ILoggerFunctions __loggerFunctions, IApiResponse __apiResponse, ISalesUserContextFunctions __userContext, ISaleFunctions __saleFunctions)
            : base(__loggerFunctions, __apiResponse, __userContext)
        {
            _saleFunctions = __saleFunctions;
        }

        /// <summary>scope=mine (default) or team (managers). Filters: sellerId (team), search, status, soldFrom, soldTo.
        /// Sort: soldAt, saleNumber, customer, product, saleAmount, renewalDate.</summary>
        [HttpGet]
        public Task<IActionResult> GetSales([FromQuery] SaleFilter _filter, CancellationToken _ct) => WithUserAsync("C:Sales:GetSales", async _user =>
        {
            var (_result, _fr) = await _saleFunctions.GetSalesAsync(_filter, _user, _ct);
            return Result(_result, _fr);
        }, _ct);

        /// <summary>The signed-in manager's team (for the Team sales filter).</summary>
        [HttpGet("team-members")]
        public Task<IActionResult> GetTeamMembers(CancellationToken _ct) => WithUserAsync("C:Sales:GetTeamMembers", async _user =>
        {
            var (_members, _fr) = await _saleFunctions.GetTeamMembersAsync(_user, _ct);
            return Result(_members, _fr);
        }, _ct);

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetSale(Guid id, CancellationToken _ct) => WithUserAsync("C:Sales:GetSale", async _user =>
        {
            var (_sale, _fr) = await _saleFunctions.GetSaleAsync(id, _user, _ct);
            return Result(_sale, _fr);
        }, _ct);

        [HttpPost]
        public Task<IActionResult> CreateSale([FromBody] HttpSalePostParams _params, CancellationToken _ct) => WithUserAsync("C:Sales:CreateSale", async _user =>
        {
            var (_sale, _fr) = await _saleFunctions.CreateSaleAsync(_params, _user, ClientIp, _ct);
            return Result(_sale, _fr);
        }, _ct);

        [HttpPost("{id:guid}/cancel")]
        public Task<IActionResult> CancelSale(Guid id, [FromBody] HttpSaleCancelParams _params, CancellationToken _ct) => WithUserAsync("C:Sales:CancelSale", async _user =>
        {
            var (_sale, _fr) = await _saleFunctions.CancelSaleAsync(id, _params, _user, ClientIp, _ct);
            return Result(_sale, _fr);
        }, _ct);
    }
}
