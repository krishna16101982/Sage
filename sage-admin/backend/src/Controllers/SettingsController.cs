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
    public class SettingsController : ControllerBase
    {
        private readonly ISettingFunctions _settingFunctions;
        private readonly IAuthorizationFunctions _authorizationFunctions;
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IApiResponse _apiResponse;

        public SettingsController(ILoggerFunctions __loggerFunctions, ISettingFunctions __settingFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
        {
            _loggerFunctions = __loggerFunctions;
            _settingFunctions = __settingFunctions;
            _authorizationFunctions = __authorizationFunctions;
            _apiResponse = __apiResponse;
        }

        private async Task<long?> ActorIdAsync(CancellationToken _ct)
            => Guid.TryParse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value, out Guid _id)
                ? await _authorizationFunctions.GetUserIdAsync(_id, _ct)
                : null;

        private async Task<IActionResult> RunAsync<T>(string _methodName, Func<Task<(T data, IFunctionReturn functionReturn)>> _action)
        {
            try
            {
                (T _data, IFunctionReturn _functionReturn) = await _action();
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                string _message = _functionReturn.Message.Count > 0 ? _functionReturn.Message[0] : "OK";
                return Ok(_apiResponse.Success(_data, _message));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
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
