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
    public class CustomersController : AdminControllerBase
    {
        private readonly ICustomerFunctions _customerFunctions;

        public CustomersController(ILoggerFunctions __loggerFunctions, ICustomerFunctions __customerFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _customerFunctions = __customerFunctions;
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
