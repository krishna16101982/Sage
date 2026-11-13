using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.Controllers
{
    /// <summary>
    /// What every Admin API controller needs: the signed-in admin, their internal id for audit rows, the client IP, and
    /// RunAsync, which turns a function's (data, FunctionReturn) into the common response envelope.
    /// An action then reads:
    /// <code>=> RunAsync("C:Customers:GetCustomer", () => _customerFunctions.GetCustomerAsync(id, _ct));</code>
    /// </summary>
    public abstract class AdminControllerBase : ControllerBase
    {
        protected readonly ILoggerFunctions _loggerFunctions;
        protected readonly IApiResponse _apiResponse;
        protected readonly IAuthorizationFunctions _authorizationFunctions;

        protected AdminControllerBase(ILoggerFunctions __loggerFunctions, IApiResponse __apiResponse, IAuthorizationFunctions __authorizationFunctions = null)
        {
            _loggerFunctions = __loggerFunctions;
            _apiResponse = __apiResponse;
            _authorizationFunctions = __authorizationFunctions;
        }

        /// <summary>The signed-in admin's PublicId (from the access token), or null.</summary>
        protected Guid? CurrentUserId => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid _id) ? _id : null;

        /// <summary>The signed-in admin's internal id, written to audit rows as the actor (null when unknown).</summary>
        protected async Task<long?> ActorIdAsync(CancellationToken _ct)
            => CurrentUserId is Guid _id && _authorizationFunctions != null ? await _authorizationFunctions.GetUserIdAsync(_id, _ct) : null;

        protected string ClientIp => ApplicationCommonFunctions.GetClientIp(HttpContext);

        /// <summary>
        /// Runs one action and returns the common envelope: the function's status code (200 when it sets none) and its first
        /// message, or its error. An unexpected exception is logged and answered with a generic 500.
        /// </summary>
        protected async Task<IActionResult> RunAsync<T>(string _methodName, Func<Task<(T data, IFunctionReturn functionReturn)>> _action)
        {
            try
            {
                (T _data, IFunctionReturn _functionReturn) = await _action();
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                string _status = string.IsNullOrEmpty(_functionReturn.HttpStatusCode) ? "200" : _functionReturn.HttpStatusCode;
                string _message = _functionReturn.Message.Count > 0 ? _functionReturn.Message[0] : "OK";
                return new ObjectResult(_apiResponse.Success(_data, _message, _status)) { StatusCode = int.Parse(_status) };
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }
    }
}
