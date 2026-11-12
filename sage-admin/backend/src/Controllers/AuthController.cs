using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Globals;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.Controllers
{
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

        #region Cookie helpers

        /// <summary>The refresh token never appears in a response body; it travels only in this HttpOnly cookie.</summary>
        private void SetRefreshCookie(string _token, DateTime _expiresUtc)
        {
            Response.Cookies.Append(ApplicationConstants.Auth.RefreshCookieName, _token, BuildCookieOptions(_expiresUtc));
        }

        private void ClearRefreshCookie()
        {
            Response.Cookies.Delete(ApplicationConstants.Auth.RefreshCookieName, BuildCookieOptions(null));
        }

        private CookieOptions BuildCookieOptions(DateTime? _expiresUtc)
        {
            SameSiteMode _sameSite = Enum.TryParse(_authSettings.RefreshCookieSameSite, true, out SameSiteMode _parsed) ? _parsed : SameSiteMode.Lax;
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = _authSettings.RefreshCookieSecure,
                SameSite = _sameSite,
                Path = ApplicationConstants.Auth.RefreshCookiePath,
                Expires = _expiresUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(_expiresUtc.Value, DateTimeKind.Utc)) : (DateTimeOffset?)null
            };
        }

        private string RefreshCookie => Request.Cookies[ApplicationConstants.Auth.RefreshCookieName];
        private string UserAgent => Request.Headers["User-Agent"].ToString();

        #endregion

        /// <summary>Admin login. Returns the access token and user; sets the refresh-token cookie.</summary>
        [HttpPost]
        [Route("login")]
        [AllowAnonymous]
        [EnableRateLimiting(ApplicationConstants.Auth.RateLimitPolicy)]
        public async Task<IActionResult> Login([FromBody] HttpLoginPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AllowAnonymous
            //*************************************************************
            string _methodName = "C:Auth:Login";
            try
            {
                (AuthResult _result, IFunctionReturn _functionReturn) = await _authFunctions.LoginAsync(_params, ApplicationCommonFunctions.GetClientIp(HttpContext), UserAgent, _ct);
                if (!_functionReturn.Status)
                {
                    return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                }
                SetRefreshCookie(_result.RefreshToken, _result.RefreshTokenExpiresAtUtc);
                return new OkObjectResult(_apiResponse.Success(_result.Response));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Exchange the refresh-token cookie for a new access token; the refresh token is rotated.</summary>
        [HttpPost]
        [Route("refresh-token")]
        [AllowAnonymous]
        [EnableRateLimiting(ApplicationConstants.Auth.RateLimitPolicy)]
        public async Task<IActionResult> RefreshToken(CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AllowAnonymous (authenticated by the refresh-token cookie)
            //*************************************************************
            string _methodName = "C:Auth:RefreshToken";
            try
            {
                (AuthResult _result, IFunctionReturn _functionReturn) = await _authFunctions.RefreshAsync(RefreshCookie, ApplicationCommonFunctions.GetClientIp(HttpContext), UserAgent, _ct);
                if (!_functionReturn.Status)
                {
                    ClearRefreshCookie();
                    return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                }
                SetRefreshCookie(_result.RefreshToken, _result.RefreshTokenExpiresAtUtc);
                return new OkObjectResult(_apiResponse.Success(_result.Response));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Revoke the refresh-token family and clear the cookie. Idempotent; works with an expired access token.</summary>
        [HttpPost]
        [Route("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout(CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AllowAnonymous (identified by the refresh-token cookie)
            //*************************************************************
            string _methodName = "C:Auth:Logout";
            try
            {
                IFunctionReturn _functionReturn = await _authFunctions.LogoutAsync(RefreshCookie, ApplicationCommonFunctions.GetClientIp(HttpContext), _ct);
                ClearRefreshCookie();
                if (!_functionReturn.Status)
                {
                    return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                }
                return new OkObjectResult(_apiResponse.Success<object>(null, _functionReturn.Message[0]));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Request a password-reset link. Always answers the same, whether or not the email exists.</summary>
        [HttpPost]
        [Route("forgot-password")]
        [AllowAnonymous]
        [EnableRateLimiting(ApplicationConstants.Auth.RateLimitPolicy)]
        public async Task<IActionResult> ForgotPassword([FromBody] HttpForgotPasswordPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AllowAnonymous
            //*************************************************************
            string _methodName = "C:Auth:ForgotPassword";
            try
            {
                IFunctionReturn _functionReturn = await _authFunctions.ForgotPasswordAsync(_params?.Email, ApplicationCommonFunctions.GetClientIp(HttpContext), _ct);
                if (!_functionReturn.Status)
                {
                    return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                }
                return new OkObjectResult(_apiResponse.Success<object>(null, _functionReturn.Message[0]));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Set a new password using a reset token. Revokes every session of the user.</summary>
        [HttpPost]
        [Route("reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting(ApplicationConstants.Auth.RateLimitPolicy)]
        public async Task<IActionResult> ResetPassword([FromBody] HttpResetPasswordPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AllowAnonymous (authenticated by the reset token)
            //*************************************************************
            string _methodName = "C:Auth:ResetPassword";
            try
            {
                IFunctionReturn _functionReturn = await _authFunctions.ResetPasswordAsync(_params, ApplicationCommonFunctions.GetClientIp(HttpContext), _ct);
                if (!_functionReturn.Status)
                {
                    return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                }
                ClearRefreshCookie();
                return new OkObjectResult(_apiResponse.Success<object>(null, _functionReturn.Message[0]));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Change the signed-in user's password. All sessions are revoked, so the user signs in again.</summary>
        [HttpPost]
        [Route("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] HttpChangePasswordPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: any authenticated admin user
            //*************************************************************
            string _methodName = "C:Auth:ChangePassword";
            try
            {
                if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid _userId))
                {
                    return Unauthorized();
                }
                IFunctionReturn _functionReturn = await _authFunctions.ChangePasswordAsync(_userId, _params, ApplicationCommonFunctions.GetClientIp(HttpContext), _ct);
                if (!_functionReturn.Status)
                {
                    return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                }
                ClearRefreshCookie();
                return new OkObjectResult(_apiResponse.Success<object>(null, _functionReturn.Message[0]));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>The signed-in user with permissions and modules (used by the frontend for the dynamic sidebar).</summary>
        [HttpGet]
        [Route("me")]
        [Authorize]
        public async Task<IActionResult> Me(CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: any authenticated admin user
            //*************************************************************
            string _methodName = "C:Auth:Me";
            try
            {
                if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid _userId))
                {
                    return Unauthorized();
                }
                (CurrentUserResponse _user, IFunctionReturn _functionReturn) = await _authFunctions.GetCurrentUserAsync(_userId, _ct);
                if (!_functionReturn.Status)
                {
                    return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                }
                return new OkObjectResult(_apiResponse.Success(_user));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }
    }
}
