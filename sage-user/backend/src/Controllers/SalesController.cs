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

        /// <summary>Cancel a sale directly: sales managers only (their own sales and their team's).</summary>
        [HttpPost("{id:guid}/cancel")]
        public Task<IActionResult> CancelSale(Guid id, [FromBody] HttpSaleCancelParams _params, CancellationToken _ct) => WithUserAsync("C:Sales:CancelSale", async _user =>
        {
            var (_sale, _fr) = await _saleFunctions.CancelSaleAsync(id, _params, _user, ClientIp, _ct);
            return Result(_sale, _fr);
        }, _ct);

        /// <summary>A sales person asks their manager to cancel one of their own sales (only when the manager allowed it).</summary>
        [HttpPost("{id:guid}/cancel-request")]
        public Task<IActionResult> RequestCancel(Guid id, [FromBody] HttpSaleCancelParams _params, CancellationToken _ct) => WithUserAsync("C:Sales:RequestCancel", async _user =>
        {
            var (_request, _fr) = await _saleFunctions.CreateCancelRequestAsync(id, _params, _user, ClientIp, _ct);
            return Result(_request, _fr);
        }, _ct);

        /// <summary>Cancellation requests: a manager sees their team's, a sales person their own. status=Pending|Approved|Rejected.</summary>
        [HttpGet("cancel-requests")]
        public Task<IActionResult> GetCancelRequests([FromQuery] string status, CancellationToken _ct) => WithUserAsync("C:Sales:GetCancelRequests", async _user =>
        {
            var (_requests, _fr) = await _saleFunctions.GetCancelRequestsAsync(status, _user, _ct);
            return Result(_requests, _fr);
        }, _ct);

        /// <summary>The manager approves a request: the sale is cancelled.</summary>
        [HttpPost("cancel-requests/{id:guid}/approve")]
        public Task<IActionResult> ApproveCancel(Guid id, [FromBody] HttpCancelDecisionParams _params, CancellationToken _ct) => WithUserAsync("C:Sales:ApproveCancel", async _user =>
        {
            var (_request, _fr) = await _saleFunctions.DecideCancelRequestAsync(id, true, _params, _user, ClientIp, _ct);
            return Result(_request, _fr);
        }, _ct);

        /// <summary>The manager rejects a request: the sale stays active.</summary>
        [HttpPost("cancel-requests/{id:guid}/reject")]
        public Task<IActionResult> RejectCancel(Guid id, [FromBody] HttpCancelDecisionParams _params, CancellationToken _ct) => WithUserAsync("C:Sales:RejectCancel", async _user =>
        {
            var (_request, _fr) = await _saleFunctions.DecideCancelRequestAsync(id, false, _params, _user, ClientIp, _ct);
            return Result(_request, _fr);
        }, _ct);

        /// <summary>The manager allows (or stops) a team member asking for cancellations.</summary>
        [HttpPut("team-members/{id:guid}/cancel-permission")]
        public Task<IActionResult> SetCancelPermission(Guid id, [FromBody] HttpCancelPermissionParams _params, CancellationToken _ct) => WithUserAsync("C:Sales:SetCancelPermission", async _user =>
        {
            var (_member, _fr) = await _saleFunctions.SetCancelPermissionAsync(id, _params, _user, ClientIp, _ct);
            return Result(_member, _fr);
        }, _ct);
    }
}
