using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SageAdminApi.AppFunctions;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.Controllers
{
    /// <summary>Leads of the User Panel, as seen from the Admin dashboard (Sales.View).</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/leads")]
    [ApiController]
    public class LeadsController : AdminControllerBase
    {
        private readonly ILeadSummaryFunctions _leadSummaryFunctions;

        public LeadsController(ILoggerFunctions __loggerFunctions, ILeadSummaryFunctions __leadSummaryFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _leadSummaryFunctions = __leadSummaryFunctions;
        }

        /// <summary>Dashboard figures: every lead, or one sales user's (?salesUserId=).</summary>
        [HttpGet("summary")]
        [RequirePermission(PermissionConstants.SalesView)]
        public Task<IActionResult> GetSummary([FromQuery] Guid? salesUserId, CancellationToken _ct)
            => RunAsync<LeadSummary>("C:Leads:GetSummary", () => _leadSummaryFunctions.GetSummaryAsync(salesUserId, _ct));
    }
}
