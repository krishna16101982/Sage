using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.AppValidations;
using SageUserApi.Models;

namespace SageUserApi.Controllers
{
    /// <summary>Shared plumbing for the signed-in sales screens: the current sales user, error envelope and logging.</summary>
    public abstract class SalesControllerBase : ControllerBase
    {
        protected readonly ILoggerFunctions _loggerFunctions;
        protected readonly IApiResponse _apiResponse;
        private readonly ISalesUserContextFunctions _userContext;

        protected SalesControllerBase(ILoggerFunctions __loggerFunctions, IApiResponse __apiResponse, ISalesUserContextFunctions __userContext)
        {
            _loggerFunctions = __loggerFunctions;
            _apiResponse = __apiResponse;
            _userContext = __userContext;
        }

        protected string ClientIp => ApplicationCommonFunctions.GetClientIp(HttpContext);

        /// <summary>Runs the action for the signed-in, active sales user; any unexpected exception becomes a 500 envelope.</summary>
        protected async Task<IActionResult> WithUserAsync(string _methodName, Func<SalesUserContext, Task<IActionResult>> _action, CancellationToken _ct)
        {
            try
            {
                if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid _publicId)) return Unauthorized();
                SalesUserContext _user = await _userContext.GetAsync(_publicId, _ct);
                if (_user == null)
                    return ApplicationCommonFunctions.ErrorResult(_apiResponse,
                        FunctionReturn.Fail(Constants.ReturnMessageTypes.TokenError, Constants.TokenMessages.UnAuthorizedAccess, Constants.HttpStatusCodes.Unauthorized, _methodName));
                return await _action(_user);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>200 (or the status the function chose, e.g. 201) with data, or the error envelope.</summary>
        protected IActionResult Result<T>(T _data, IFunctionReturn _functionReturn)
        {
            if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
            string _message = _functionReturn.Message != null && _functionReturn.Message.Count > 0 ? _functionReturn.Message[0] : "OK";
            string _httpStatus = string.IsNullOrEmpty(_functionReturn.HttpStatusCode) ? Constants.HttpStatusCodes.Ok : _functionReturn.HttpStatusCode;
            int _status = int.TryParse(_httpStatus, out int _parsed) ? _parsed : 200;
            return new ObjectResult(_apiResponse.Success(_data, _message, _httpStatus)) { StatusCode = _status };
        }
    }
}
