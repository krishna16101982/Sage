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
    /// <summary>Order form lists (dbo.MasterValues). Part of Settings: Setting.View to see them, Setting.Edit to change them.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/masters")]
    [ApiController]
    public class MastersController : AdminControllerBase
    {
        private readonly IMasterValueFunctions _masterValueFunctions;

        public MastersController(ILoggerFunctions __loggerFunctions, IMasterValueFunctions __masterValueFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _masterValueFunctions = __masterValueFunctions;
        }

        /// <summary>Every list with all its values (active and turned off), defaults marked, and how many orders use each.</summary>
        [HttpGet]
        [RequirePermission(PermissionConstants.SettingView)]
        public Task<IActionResult> GetLists(CancellationToken _ct)
            => RunAsync<List<MasterList>>("C:Masters:GetLists", () => _masterValueFunctions.GetListsAsync(_ct));

        [HttpPost]
        [RequirePermission(PermissionConstants.SettingEdit)]
        public Task<IActionResult> Create([FromBody] HttpMasterValuePostParams _params, CancellationToken _ct)
            => RunAsync<MasterValueDto>("C:Masters:Create", async () => await _masterValueFunctions.SaveAsync(null, _params, await ActorIdAsync(_ct), ClientIp, _ct));

        [HttpPut("{id:int}")]
        [RequirePermission(PermissionConstants.SettingEdit)]
        public Task<IActionResult> Update(int id, [FromBody] HttpMasterValuePostParams _params, CancellationToken _ct)
            => RunAsync<MasterValueDto>("C:Masters:Update", async () => await _masterValueFunctions.SaveAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct));
    }
}
