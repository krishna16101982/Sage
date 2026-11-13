using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SageAdminApi.AppFunctions;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.Filter;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.Controllers
{
    /// <summary>Sales recorded in the User Panel, read-only: by sales person, manager-wise, and totals per person / manager.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/sales")]
    [ApiController]
    public class SalesController : ControllerBase
    {
        private readonly ISalesReportFunctions _salesReportFunctions;
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IApiResponse _apiResponse;

        public SalesController(ILoggerFunctions __loggerFunctions, ISalesReportFunctions __salesReportFunctions, IApiResponse __apiResponse)
        {
            _loggerFunctions = __loggerFunctions;
            _salesReportFunctions = __salesReportFunctions;
            _apiResponse = __apiResponse;
        }

        private async Task<IActionResult> RunAsync<T>(string _methodName, Func<Task<(T data, IFunctionReturn functionReturn)>> _action)
        {
            try
            {
                (T _data, IFunctionReturn _functionReturn) = await _action();
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return Ok(_apiResponse.Success(_data));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>All sales. Filters: sellerId, managerId (the manager's own sales + their team's), customerId, search, status,
        /// soldFrom, soldTo. Sort: soldAt, saleNumber, customer, product, seller, saleAmount, renewalDate. Totals cover active sales.</summary>
        [HttpGet]
        [RequirePermission(PermissionConstants.SalesView)]
        public Task<IActionResult> GetSales([FromQuery] AdminSaleFilter _filter, CancellationToken _ct)
            => RunAsync<AdminSalesPage>("C:Sales:GetSales", () => _salesReportFunctions.GetSalesAsync(_filter, _ct));

        /// <summary>Totals per sales person (groupBy=seller) or per manager and team (groupBy=manager), for soldFrom..soldTo.</summary>
        [HttpGet("summary")]
        [RequirePermission(PermissionConstants.SalesView)]
        public Task<IActionResult> GetSummary([FromQuery] SalesSummaryFilter _filter, CancellationToken _ct)
            => RunAsync<List<SalesSummaryRow>>("C:Sales:GetSummary", () => _salesReportFunctions.GetSummaryAsync(_filter, _ct));

        /// <summary>Sales persons and managers, for the filters.</summary>
        [HttpGet("sellers")]
        [RequirePermission(PermissionConstants.SalesView)]
        public Task<IActionResult> GetSellers(CancellationToken _ct)
            => RunAsync<List<SellerOption>>("C:Sales:GetSellers", () => _salesReportFunctions.GetSellersAsync(_ct));

        [HttpGet("{id:guid}")]
        [RequirePermission(PermissionConstants.SalesView)]
        public Task<IActionResult> GetSale(Guid id, CancellationToken _ct)
            => RunAsync<AdminSaleDto>("C:Sales:GetSale", () => _salesReportFunctions.GetSaleAsync(id, _ct));
    }
}
