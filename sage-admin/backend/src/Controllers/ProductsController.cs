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
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.Controllers
{
    /// <summary>Product catalog: categories, products and plans (price list).</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/products")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductFunctions _productFunctions;
        private readonly IAuthorizationFunctions _authorizationFunctions;
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IApiResponse _apiResponse;

        public ProductsController(ILoggerFunctions __loggerFunctions, IProductFunctions __productFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
        {
            _loggerFunctions = __loggerFunctions;
            _productFunctions = __productFunctions;
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

        /// <summary>Runs one action and turns its result into the common envelope (status code comes from the function).</summary>
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

        /// <summary>Categories with their products and plans. includeInactive=true for management screens.</summary>
        [HttpGet]
        [Route("catalog")]
        [RequirePermission(PermissionConstants.ProductView)]
        public Task<IActionResult> GetCatalog([FromQuery] bool includeInactive, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Product.View
            //*************************************************************
            return RunAsync<List<ProductCategoryDto>>("C:Products:GetCatalog", () => _productFunctions.GetCatalogAsync(includeInactive, _ct));
        }

        /// <summary>Create a category.</summary>
        [HttpPost]
        [Route("categories")]
        [RequirePermission(PermissionConstants.ProductAdd)]
        public Task<IActionResult> CreateCategory([FromBody] HttpProductCategoryPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Product.Add
            //*************************************************************
            return RunAsync<ProductCategoryDto>("C:Products:CreateCategory", async () => await _productFunctions.CreateCategoryAsync(_params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Edit a category (name, description, order, active). The code never changes.</summary>
        [HttpPut]
        [Route("categories/{id:long}")]
        [RequirePermission(PermissionConstants.ProductEdit)]
        public Task<IActionResult> UpdateCategory(long id, [FromBody] HttpProductCategoryPutParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Product.Edit
            //*************************************************************
            return RunAsync<ProductCategoryDto>("C:Products:UpdateCategory", async () => await _productFunctions.UpdateCategoryAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Create a product in an active category.</summary>
        [HttpPost]
        [RequirePermission(PermissionConstants.ProductAdd)]
        public Task<IActionResult> CreateProduct([FromBody] HttpProductPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Product.Add
            //*************************************************************
            return RunAsync<ProductDto>("C:Products:CreateProduct", async () => await _productFunctions.CreateProductAsync(_params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Edit a product (category, name, description, order, active). The code never changes.</summary>
        [HttpPut]
        [Route("{id:long}")]
        [RequirePermission(PermissionConstants.ProductEdit)]
        public Task<IActionResult> UpdateProduct(long id, [FromBody] HttpProductPutParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Product.Edit
            //*************************************************************
            return RunAsync<ProductDto>("C:Products:UpdateProduct", async () => await _productFunctions.UpdateProductAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Add a plan (price-list entry) to an active product.</summary>
        [HttpPost]
        [Route("{productId:long}/plans")]
        [RequirePermission(PermissionConstants.ProductAdd)]
        public Task<IActionResult> CreatePlan(long productId, [FromBody] HttpProductPlanPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Product.Add
            //*************************************************************
            return RunAsync<ProductPlanDto>("C:Products:CreatePlan", async () => await _productFunctions.CreatePlanAsync(productId, _params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Edit a plan, including its price and cost (affects future sales only).</summary>
        [HttpPut]
        [Route("plans/{id:long}")]
        [RequirePermission(PermissionConstants.ProductEdit)]
        public Task<IActionResult> UpdatePlan(long id, [FromBody] HttpProductPlanPutParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Product.Edit
            //*************************************************************
            return RunAsync<ProductPlanDto>("C:Products:UpdatePlan", async () => await _productFunctions.UpdatePlanAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct));
        }
    }
}
