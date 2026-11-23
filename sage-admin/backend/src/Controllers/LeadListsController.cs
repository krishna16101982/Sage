using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SageAdminApi.AppFunctions;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.Controllers
{
    /// <summary>Lead lists (products, lead sources, billing cycles). Part of Settings: Setting.View to see, Setting.Edit to change.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/lead-lists")]
    [ApiController]
    public class LeadListsController : AdminControllerBase
    {
        private readonly ILeadListFunctions _leadListFunctions;

        public LeadListsController(ILoggerFunctions __loggerFunctions, ILeadListFunctions __leadListFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _leadListFunctions = __leadListFunctions;
        }

        /// <summary>The three lists with all their values (on and off) and how many leads use each.</summary>
        [HttpGet]
        [RequirePermission(PermissionConstants.SettingView)]
        public Task<IActionResult> GetLists(CancellationToken _ct)
            => RunAsync<List<LeadList>>("C:LeadLists:GetLists", () => _leadListFunctions.GetListsAsync(_ct));

        [HttpPost]
        [RequirePermission(PermissionConstants.SettingEdit)]
        public Task<IActionResult> Create([FromBody] HttpLeadListValuePostParams _params, CancellationToken _ct)
            => RunAsync<LeadListValueDto>("C:LeadLists:Create", async () => await _leadListFunctions.SaveAsync(null, _params, await ActorIdAsync(_ct), ClientIp, _ct));

        [HttpPut("{id:int}")]
        [RequirePermission(PermissionConstants.SettingEdit)]
        public Task<IActionResult> Update(int id, [FromBody] HttpLeadListValuePostParams _params, CancellationToken _ct)
            => RunAsync<LeadListValueDto>("C:LeadLists:Update", async () => await _leadListFunctions.SaveAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct));
    }
}
