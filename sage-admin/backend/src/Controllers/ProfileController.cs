using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
    /// <summary>
    /// My Profile: every signed-in admin can see their own account and recent activity and change their own name and
    /// mobile number (no permission needed). The password is changed with POST api/admin/auth/change-password.
    /// </summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/profile")]
    [ApiController]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IAdminUserFunctions _adminUserFunctions;
        private readonly IAuthorizationFunctions _authorizationFunctions;
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IApiResponse _apiResponse;

        public ProfileController(ILoggerFunctions __loggerFunctions, IAdminUserFunctions __adminUserFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
        {
            _loggerFunctions = __loggerFunctions;
            _adminUserFunctions = __adminUserFunctions;
            _authorizationFunctions = __authorizationFunctions;
            _apiResponse = __apiResponse;
        }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid _id) ? _id : null;

        private async Task<IActionResult> RunAsync(string _methodName, Func<Guid, Task<(ProfileDto data, IFunctionReturn functionReturn)>> _action)
        {
            try
            {
                if (CurrentUserId is not Guid _userId) return Unauthorized();
                (ProfileDto _data, IFunctionReturn _functionReturn) = await _action(_userId);
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
        public Task<IActionResult> GetProfile(CancellationToken _ct)
            => RunAsync("C:Profile:GetProfile", _userId => _adminUserFunctions.GetProfileAsync(_userId, _ct));

        [HttpPut]
        public Task<IActionResult> UpdateProfile([FromBody] HttpProfilePutParams _params, CancellationToken _ct)
            => RunAsync("C:Profile:UpdateProfile", async _userId => await _adminUserFunctions.UpdateProfileAsync(_userId, _params,
                await _authorizationFunctions.GetUserIdAsync(_userId, _ct), ApplicationCommonFunctions.GetClientIp(HttpContext), _ct));
    }
}
