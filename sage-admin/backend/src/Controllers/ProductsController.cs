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
    public class ProductsController : AdminControllerBase
    {
        private readonly IProductFunctions _productFunctions;

        public ProductsController(ILoggerFunctions __loggerFunctions, IProductFunctions __productFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _productFunctions = __productFunctions;
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
