using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SageAdminApi.AppFunctions.Interfaces;
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
    public class ProfileController : AdminControllerBase
    {
        private readonly IAdminUserFunctions _adminUserFunctions;

        public ProfileController(ILoggerFunctions __loggerFunctions, IAdminUserFunctions __adminUserFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _adminUserFunctions = __adminUserFunctions;
        }

        [HttpGet]
        public async Task<IActionResult> GetProfile(CancellationToken _ct)
        {
            if (CurrentUserId is not { } _userId) return Unauthorized();
            return await RunAsync<ProfileDto>("C:Profile:GetProfile", () => _adminUserFunctions.GetProfileAsync(_userId, _ct));
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] HttpProfilePutParams _params, CancellationToken _ct)
        {
            if (CurrentUserId is not { } _userId) return Unauthorized();
            return await RunAsync<ProfileDto>("C:Profile:UpdateProfile",
                async () => await _adminUserFunctions.UpdateProfileAsync(_userId, _params, await ActorIdAsync(_ct), ClientIp, _ct));
        }
    }
}
