using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SageAdminApi.AppFunctions;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.Controllers
{
    /// <summary>Application settings (dbo.Settings): list and change values.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/settings")]
    [ApiController]
    public class SettingsController : AdminControllerBase
    {
        private readonly ISettingFunctions _settingFunctions;

        public SettingsController(ILoggerFunctions __loggerFunctions, ISettingFunctions __settingFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _settingFunctions = __settingFunctions;
        }

        [HttpGet]
        [RequirePermission(PermissionConstants.SettingView)]
        public Task<IActionResult> GetSettings(CancellationToken _ct)
            => RunAsync<List<SettingDto>>("C:Settings:GetSettings", () => _settingFunctions.GetSettingsAsync(_ct));

        /// <summary>Change one value; it is checked against the setting's type and limits.</summary>
        [HttpPut("{key}")]
        [RequirePermission(PermissionConstants.SettingEdit)]
        public Task<IActionResult> UpdateSetting(string key, [FromBody] HttpSettingPutParams _params, CancellationToken _ct)
            => RunAsync<SettingDto>("C:Settings:UpdateSetting", async () => await _settingFunctions.UpdateSettingAsync(key, _params, await ActorIdAsync(_ct), ApplicationCommonFunctions.GetClientIp(HttpContext), _ct));
    }
}
