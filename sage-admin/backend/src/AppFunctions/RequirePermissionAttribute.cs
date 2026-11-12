using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using SageAdminApi.AppFunctions.Interfaces;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Globals;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Declares the permission an endpoint needs, e.g. [RequirePermission(PermissionConstants.GroupView)].
    /// The backend checks it against SQL on every request: 401 without a valid session, 403 without the permission.
    /// The frontend hiding a button is a convenience only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _permissionCode;

        public RequirePermissionAttribute(string __permissionCode)
        {
            _permissionCode = __permissionCode;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext _context)
        {
            IApiResponse _apiResponse = _context.HttpContext.RequestServices.GetRequiredService<IApiResponse>();

            if (!Guid.TryParse(_context.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid _userId))
            {
                _context.Result = new ObjectResult(_apiResponse.Error(Constants.ReturnMessageTypes.TokenError, new[] { Constants.TokenMessages.UnAuthorizedAccess }, Constants.HttpStatusCodes.Unauthorized)) { StatusCode = 401 };
                return;
            }

            IAuthorizationFunctions _authorizationFunctions = _context.HttpContext.RequestServices.GetRequiredService<IAuthorizationFunctions>();
            if (!await _authorizationFunctions.HasPermissionAsync(_userId, _permissionCode, _context.HttpContext.RequestAborted))
            {
                ILoggerFunctions _loggerFunctions = _context.HttpContext.RequestServices.GetRequiredService<ILoggerFunctions>();
                _loggerFunctions.Warning("A:RequirePermission", $"Permission denied: user {_userId} needs {_permissionCode} for {_context.HttpContext.Request.Method} {_context.HttpContext.Request.Path}");
                _context.Result = new ObjectResult(_apiResponse.Error(Constants.ReturnMessageTypes.PermissionDenied, new[] { Constants.GenericMessages.PermissionDenied }, Constants.HttpStatusCodes.Forbidden)) { StatusCode = 403 };
            }
        }
    }
}
