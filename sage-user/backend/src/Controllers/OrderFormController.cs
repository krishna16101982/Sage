using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Globals;
using SageUserApi.Models;
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
        private readonly IOrderFormPdfFunctions _orderFormPdfFunctions;
        private readonly ILeadFunctions _leadFunctions;

        public OrderFormController(ILoggerFunctions __loggerFunctions, IApiResponse __apiResponse, ISalesUserContextFunctions __userContext, IOrderFormFunctions __orderFormFunctions,
                                   IOrderFormPdfFunctions __orderFormPdfFunctions, ILeadFunctions __leadFunctions)
            : base(__loggerFunctions, __apiResponse, __userContext)
        {
            _orderFormFunctions = __orderFormFunctions;
            _orderFormPdfFunctions = __orderFormPdfFunctions;
            _leadFunctions = __leadFunctions;
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
            // The order form PDF is made and saved by order number right away (a failure is logged; it is made again when opened).
            if (_fr.Status && _sale != null) await _orderFormPdfFunctions.CreateAsync(_sale.Id, _ct);
            if (_fr.Status && _sale != null)
            {
                if (_params?.LeadId is Guid _leadId)
                {
                    // Made from a lead: the lead now shows this order.
                    await _leadFunctions.LinkOrderAsync(_leadId, _sale.Id, _user, _ct);
                }
                else
                {
                    // "No lead": a lead is created for the same company (its other fields are filled in later).
                    LeadDto _lead = await _leadFunctions.CreateForOrderAsync(_sale.Id, _sale.CustomerId, _params?.PrimaryContact, _user, ClientIp, _ct);
                    if (_lead != null)
                    {
                        _sale.LeadId = _lead.Id;
                        _sale.LeadNumber = _lead.LeadNumber;
                    }
                }
            }
            return Result(_sale, _fr);
        }, _ct);

        /// <summary>
        /// Edits an order saved with the order form: the seller or their manager, while it is active with no cancellation
        /// request waiting. Same body and checks as saving one; the sale number stays and the PDF is made again.
        /// </summary>
        [HttpPut("{saleId:guid}")]
        public Task<IActionResult> UpdateOrder(Guid saleId, [FromBody] HttpOrderFormPostParams _params, CancellationToken _ct) => WithUserAsync("C:OrderForm:UpdateOrder", async _user =>
        {
            var (_sale, _fr) = await _orderFormFunctions.UpdateOrderAsync(saleId, _params, _user, ClientIp, _ct);
            if (_fr.Status && _sale != null) await _orderFormPdfFunctions.CreateAsync(_sale.Id, _ct);
            return Result(_sale, _fr);
        }, _ct);

        /// <summary>
        /// Order forms list: changes the tracking status. Cancelled (with reason) cancels the sale for a manager, or sends a
        /// cancellation request for a sales person their manager allowed.
        /// </summary>
        [HttpPut("{saleId:guid}/tracking")]
        public Task<IActionResult> SetTracking(Guid saleId, [FromBody] HttpOrderTrackingParams _params, CancellationToken _ct) => WithUserAsync("C:OrderForm:SetTracking", async _user =>
        {
            var (_sale, _fr) = await _orderFormFunctions.SetTrackingAsync(saleId, _params, _user, ClientIp, _ct);
            return Result(_sale, _fr);
        }, _ct);

        /// <summary>The order-form details of a sale the user may see (hasOrderForm = false for a sale from the simple page).</summary>
        [HttpGet("{saleId:guid}")]
        public Task<IActionResult> GetOrderDetails(Guid saleId, CancellationToken _ct) => WithUserAsync("C:OrderForm:GetOrderDetails", async _user =>
        {
            var (_details, _fr) = await _orderFormFunctions.GetOrderDetailsAsync(saleId, _user, _ct);
            return Result(_details, _fr);
        }, _ct);

        /// <summary>The order form PDF ({SaleNumber}.pdf). download=true saves it; otherwise it opens in the browser (preview).</summary>
        [HttpGet("{saleId:guid}/pdf")]
        public Task<IActionResult> GetPdf(Guid saleId, [FromQuery] bool download, CancellationToken _ct) => WithUserAsync("C:OrderForm:GetPdf", async _user =>
        {
            var (_file, _fr) = await _orderFormPdfFunctions.GetAsync(saleId, _user, _ct);
            if (!_fr.Status) return Result<object>(null, _fr);
            Response.Headers.CacheControl = "no-store";
            if (download) return File(_file.Content, "application/pdf", _file.FileName);
            Response.Headers.ContentDisposition = $"inline; filename=\"{_file.FileName}\"";
            return File(_file.Content, "application/pdf");
        }, _ct);
    }
}
