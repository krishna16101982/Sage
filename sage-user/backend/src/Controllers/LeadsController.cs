using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Globals;
using SageUserApi.Models.Filter;
using SageUserApi.Models.HttpPost;

namespace SageUserApi.Controllers
{
    /// <summary>
    /// Lead Records. Every sales user sees every lead (paged in dbo.usp_Lead_GetPaged). The creator and the creator's manager
    /// change a lead (its company stays); sales managers delete their own and their team's leads (hidden, never removed).
    /// </summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/leads")]
    [ApiController]
    [Authorize]
    public class LeadsController : SalesControllerBase
    {
        private readonly ILeadFunctions _leadFunctions;

        public LeadsController(ILoggerFunctions __loggerFunctions, IApiResponse __apiResponse, ISalesUserContextFunctions __userContext, ILeadFunctions __leadFunctions)
            : base(__loggerFunctions, __apiResponse, __userContext)
        {
            _leadFunctions = __leadFunctions;
        }

        /// <summary>One page. Filters: search, statusId, productId, assignedId, generatedById, aging (lt7, 7to30, 31to60, gt60), from, to.
        /// Sort: leadNumber (default, newest first), leadDate, company, revenue, aging.</summary>
        [HttpGet]
        public Task<IActionResult> GetLeads([FromQuery] LeadFilter _filter, CancellationToken _ct) => WithUserAsync("C:Leads:GetLeads", async _user =>
        {
            var (_result, _fr) = await _leadFunctions.GetLeadsAsync(_filter, _user, _ct);
            return Result(_result, _fr);
        }, _ct);

        /// <summary>Home dashboard: lead counts by status, source, aging and type, conversion rate, won revenue (and per product).
        /// mine=true counts only the leads the user created, generated or is assigned to.</summary>
        [HttpGet("summary")]
        public Task<IActionResult> GetSummary([FromQuery] bool mine, CancellationToken _ct) => WithUserAsync("C:Leads:GetSummary", async _user =>
        {
            var (_summary, _fr) = await _leadFunctions.GetSummaryAsync(mine, _user, _ct);
            return Result(_summary, _fr);
        }, _ct);

        /// <summary>The lists of the lead form and filters, and the sales users.</summary>
        [HttpGet("options")]
        public Task<IActionResult> GetOptions(CancellationToken _ct) => WithUserAsync("C:Leads:GetOptions", async _ =>
        {
            var (_options, _fr) = await _leadFunctions.GetOptionsAsync(_ct);
            return Result(_options, _fr);
        }, _ct);

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetLead(Guid id, CancellationToken _ct) => WithUserAsync("C:Leads:GetLead", async _user =>
        {
            var (_lead, _fr) = await _leadFunctions.GetLeadAsync(id, _user, _ct);
            return Result(_lead, _fr);
        }, _ct);

        [HttpPost]
        public Task<IActionResult> CreateLead([FromBody] HttpLeadPostParams _params, CancellationToken _ct) => WithUserAsync("C:Leads:CreateLead", async _user =>
        {
            var (_lead, _fr) = await _leadFunctions.SaveLeadAsync(null, _params, _user, ClientIp, _ct);
            return Result(_lead, _fr);
        }, _ct);

        [HttpPut("{id:guid}")]
        public Task<IActionResult> UpdateLead(Guid id, [FromBody] HttpLeadPostParams _params, CancellationToken _ct) => WithUserAsync("C:Leads:UpdateLead", async _user =>
        {
            var (_lead, _fr) = await _leadFunctions.SaveLeadAsync(id, _params, _user, ClientIp, _ct);
            return Result(_lead, _fr);
        }, _ct);

        [HttpDelete("{id:guid}")]
        public Task<IActionResult> DeleteLead(Guid id, CancellationToken _ct) => WithUserAsync("C:Leads:DeleteLead", async _user =>
        {
            var _fr = await _leadFunctions.DeleteLeadAsync(id, _user, ClientIp, _ct);
            return Result<object>(null, _fr);
        }, _ct);
    }
}
