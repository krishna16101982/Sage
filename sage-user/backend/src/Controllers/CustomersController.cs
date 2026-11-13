using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Globals;
using SageUserApi.Models.Filter;
using SageUserApi.Models.HttpPost;

namespace SageUserApi.Controllers
{
    /// <summary>Customers, shared by all sales users: search and add.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/customers")]
    [ApiController]
    public class CustomersController : SalesControllerBase
    {
        private readonly ICustomerFunctions _customerFunctions;

        public CustomersController(ILoggerFunctions __loggerFunctions, IApiResponse __apiResponse, ISalesUserContextFunctions __userContext, ICustomerFunctions __customerFunctions)
            : base(__loggerFunctions, __apiResponse, __userContext)
        {
            _customerFunctions = __customerFunctions;
        }

        /// <summary>Search customers (name, contact, email, phone, GSTIN, city). Sort: name, city, createdAtUtc.</summary>
        [HttpGet]
        public Task<IActionResult> GetCustomers([FromQuery] CustomerFilter _filter, CancellationToken _ct) => WithUserAsync("C:Customers:GetCustomers", async _ =>
        {
            var (_result, _fr) = await _customerFunctions.GetCustomersAsync(_filter, _ct);
            return Result(_result, _fr);
        }, _ct);

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetCustomer(Guid id, CancellationToken _ct) => WithUserAsync("C:Customers:GetCustomer", async _ =>
        {
            var (_customer, _fr) = await _customerFunctions.GetCustomerAsync(id, _ct);
            return Result(_customer, _fr);
        }, _ct);

        [HttpPost]
        public Task<IActionResult> CreateCustomer([FromBody] HttpCustomerPostParams _params, CancellationToken _ct) => WithUserAsync("C:Customers:CreateCustomer", async _user =>
        {
            var (_customer, _fr) = await _customerFunctions.CreateCustomerAsync(_params, _user, ClientIp, _ct);
            return Result(_customer, _fr);
        }, _ct);
    }
}
