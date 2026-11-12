using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.AppValidations;
using SageUserApi.Globals;
using SageUserApi.Models;
using SageUserApi.Models.HttpPost;

namespace SageUserApi.Controllers
{
    /// <summary>User Panel sign-in for sales users. The refresh token travels only in an HttpOnly cookie.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthFunctions _authFunctions;
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IApiResponse _apiResponse;
        private readonly AuthSettings _authSettings;

        public AuthController(ILoggerFunctions __loggerFunctions, IAuthFunctions __authFunctions, IApiResponse __apiResponse, IOptions<AuthSettings> __authSettings)
        {
            _loggerFunctions = __loggerFunctions;
            _authFunctions = __authFunctions;
            _apiResponse = __apiResponse;
            _authSettings = __authSettings.Value;
        }

        #region Helpers

        private CookieOptions CookieOptions(DateTime? _expiresUtc) => new CookieOptions
        {
            HttpOnly = true,
            Secure = _authSettings.RefreshCookieSecure,
            SameSite = Enum.TryParse(_authSettings.RefreshCookieSameSite, true, out SameSiteMode _mode) ? _mode : SameSiteMode.Lax,
            Path = ApplicationConstants.Auth.RefreshCookiePath,
            Expires = _expiresUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(_expiresUtc.Value, DateTimeKind.Utc)) : null
        };

        private void SetRefreshCookie(AuthResult _result) => Response.Cookies.Append(ApplicationConstants.Auth.RefreshCookieName, _result.RefreshToken, CookieOptions(_result.RefreshTokenExpiresAtUtc));
        private void ClearRefreshCookie() => Response.Cookies.Delete(ApplicationConstants.Auth.RefreshCookieName, CookieOptions(null));
        private string RefreshCookie => Request.Cookies[ApplicationConstants.Auth.RefreshCookieName];
        private string UserAgent => Request.Headers["User-Agent"].ToString();
        private string ClientIp => ApplicationCommonFunctions.GetClientIp(HttpContext);
        private bool TryGetUserId(out Guid _id) => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out _id);

        private async Task<IActionResult> RunAsync(string _methodName, Func<Task<IActionResult>> _action)
        {
            try { return await _action(); }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        #endregion

        /// <summary>Sales user sign-in. Returns the access token and user; sets the refresh-token cookie.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(ApplicationConstants.Auth.RateLimitPolicy)]
        public Task<IActionResult> Login([FromBody] HttpLoginPostParams _params, CancellationToken _ct) => RunAsync("C:Auth:Login", async () =>
        {
            (AuthResult _result, IFunctionReturn _fr) = await _authFunctions.LoginAsync(_params, ClientIp, UserAgent, _ct);
            if (!_fr.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _fr);
            SetRefreshCookie(_result);
            return Ok(_apiResponse.Success(_result.Response));
        });

        /// <summary>Exchange the refresh cookie for a new access token; the refresh token is rotated.</summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        [EnableRateLimiting(ApplicationConstants.Auth.RateLimitPolicy)]
        public Task<IActionResult> RefreshToken(CancellationToken _ct) => RunAsync("C:Auth:RefreshToken", async () =>
        {
            (AuthResult _result, IFunctionReturn _fr) = await _authFunctions.RefreshAsync(RefreshCookie, ClientIp, UserAgent, _ct);
            if (!_fr.Status)
            {
                ClearRefreshCookie();
                return ApplicationCommonFunctions.ErrorResult(_apiResponse, _fr);
            }
            SetRefreshCookie(_result);
            return Ok(_apiResponse.Success(_result.Response));
        });

        /// <summary>End this session (revokes the refresh-token family) and clear the cookie. Idempotent.</summary>
        [HttpPost("logout")]
        [AllowAnonymous]
        public Task<IActionResult> Logout(CancellationToken _ct) => RunAsync("C:Auth:Logout", async () =>
        {
            IFunctionReturn _fr = await _authFunctions.LogoutAsync(RefreshCookie, _ct);
            ClearRefreshCookie();
            return _fr.Status ? Ok(_apiResponse.Success<object>(null, _fr.Message[0])) : ApplicationCommonFunctions.ErrorResult(_apiResponse, _fr);
        });

        /// <summary>Change the signed-in sales user's password. Every session ends, so they sign in again.</summary>
        [HttpPost("change-password")]
        [Authorize]
        public Task<IActionResult> ChangePassword([FromBody] HttpChangePasswordPostParams _params, CancellationToken _ct) => RunAsync("C:Auth:ChangePassword", async () =>
        {
            if (!TryGetUserId(out Guid _id)) return Unauthorized();
            IFunctionReturn _fr = await _authFunctions.ChangePasswordAsync(_id, _params, ClientIp, _ct);
            if (!_fr.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _fr);
            ClearRefreshCookie();
            return Ok(_apiResponse.Success<object>(null, _fr.Message[0]));
        });

        /// <summary>The signed-in sales user (role, manager).</summary>
        [HttpGet("me")]
        [Authorize]
        public Task<IActionResult> Me(CancellationToken _ct) => RunAsync("C:Auth:Me", async () =>
        {
            if (!TryGetUserId(out Guid _id)) return Unauthorized();
            (CurrentSalesUserResponse _user, IFunctionReturn _fr) = await _authFunctions.GetCurrentUserAsync(_id, _ct);
            return _fr.Status ? Ok(_apiResponse.Success(_user)) : ApplicationCommonFunctions.ErrorResult(_apiResponse, _fr);
        });
    }
}
