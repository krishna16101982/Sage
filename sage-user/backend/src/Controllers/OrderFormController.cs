using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Globals;
using SageUserApi.Models.HttpPost;

namespace SageUserApi.Controllers
{
    /// <summary>
    /// The "New order form": its lists and defaults, saving an order (a sale with GST, free months, billing and payment
    /// terms, contacts and bill-to address) and reading those details for a sale. POST api/user/sales is unchanged.
    /// </summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/order-form")]
    [ApiController]
    [Authorize]
    public class OrderFormController : SalesControllerBase
    {
        private readonly IOrderFormFunctions _orderFormFunctions;

        public OrderFormController(ILoggerFunctions __loggerFunctions, IApiResponse __apiResponse, ISalesUserContextFunctions __userContext, IOrderFormFunctions __orderFormFunctions)
            : base(__loggerFunctions, __apiResponse, __userContext)
        {
            _orderFormFunctions = __orderFormFunctions;
        }

        /// <summary>Billing / payment lists (active values, defaults marked), default GST, most free months, discount and backdating limits.</summary>
        [HttpGet("options")]
        public Task<IActionResult> GetOptions(CancellationToken _ct) => WithUserAsync("C:OrderForm:GetOptions", async _user =>
        {
            var (_options, _fr) = await _orderFormFunctions.GetOptionsAsync(_ct);
            return Result(_options, _fr);
        }, _ct);

        /// <summary>Saves an order: a sale (same price-list rules as POST api/user/sales) with its order-form details. 201 with the sale.</summary>
        [HttpPost]
        public Task<IActionResult> CreateOrder([FromBody] HttpOrderFormPostParams _params, CancellationToken _ct) => WithUserAsync("C:OrderForm:CreateOrder", async _user =>
        {
            var (_sale, _fr) = await _orderFormFunctions.CreateOrderAsync(_params, _user, ClientIp, _ct);
            return Result(_sale, _fr);
        }, _ct);

        /// <summary>The order-form details of a sale the user may see (hasOrderForm = false for a sale from the simple page).</summary>
        [HttpGet("{saleId:guid}")]
        public Task<IActionResult> GetOrderDetails(Guid saleId, CancellationToken _ct) => WithUserAsync("C:OrderForm:GetOrderDetails", async _user =>
        {
            var (_details, _fr) = await _orderFormFunctions.GetOrderDetailsAsync(saleId, _user, _ct);
            return Result(_details, _fr);
        }, _ct);
    }
}
