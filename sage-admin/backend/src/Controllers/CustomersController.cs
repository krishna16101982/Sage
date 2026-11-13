using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SageAdminApi.AppFunctions;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.Filter;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.Controllers
{
    /// <summary>Customers (shared with the User Panel): list, add, edit.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/customers")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerFunctions _customerFunctions;
        private readonly IAuthorizationFunctions _authorizationFunctions;
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IApiResponse _apiResponse;

        public CustomersController(ILoggerFunctions __loggerFunctions, ICustomerFunctions __customerFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
        {
            _loggerFunctions = __loggerFunctions;
            _customerFunctions = __customerFunctions;
            _authorizationFunctions = __authorizationFunctions;
            _apiResponse = __apiResponse;
        }

        private async Task<long?> ActorIdAsync(CancellationToken _ct)
        {
            return Guid.TryParse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value, out Guid _id)
                ? await _authorizationFunctions.GetUserIdAsync(_id, _ct)
                : null;
        }

        private string ClientIp => ApplicationCommonFunctions.GetClientIp(HttpContext);

        private async Task<IActionResult> RunAsync<T>(string _methodName, Func<Task<(T data, IFunctionReturn functionReturn)>> _action)
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

        /// <summary>Paged list: search (name, contact, email, phone, GSTIN, city), sortBy name / city / createdAtUtc.</summary>
        [HttpGet]
        [RequirePermission(PermissionConstants.CustomerView)]
        public Task<IActionResult> GetCustomers([FromQuery] CustomerFilter _filter, CancellationToken _ct)
            => RunAsync<PagedResult<CustomerDto>>("C:Customers:GetCustomers", () => _customerFunctions.GetCustomersAsync(_filter, _ct));

        [HttpGet("{id:guid}")]
        [RequirePermission(PermissionConstants.CustomerView)]
        public Task<IActionResult> GetCustomer(Guid id, CancellationToken _ct)
            => RunAsync<CustomerDto>("C:Customers:GetCustomer", () => _customerFunctions.GetCustomerAsync(id, _ct));

        [HttpPost]
        [RequirePermission(PermissionConstants.CustomerAdd)]
        public Task<IActionResult> CreateCustomer([FromBody] HttpCustomerPostParams _params, CancellationToken _ct)
            => RunAsync<CustomerDto>("C:Customers:CreateCustomer", async () => await _customerFunctions.CreateCustomerAsync(_params, await ActorIdAsync(_ct), ClientIp, _ct));

        [HttpPut("{id:guid}")]
        [RequirePermission(PermissionConstants.CustomerEdit)]
        public Task<IActionResult> UpdateCustomer(Guid id, [FromBody] HttpCustomerPostParams _params, CancellationToken _ct)
            => RunAsync<CustomerDto>("C:Customers:UpdateCustomer", async () => await _customerFunctions.UpdateCustomerAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct));
    }
}
