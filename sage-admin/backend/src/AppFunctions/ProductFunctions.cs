using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Product catalog: categories, products and plans (the price list the User Panel sells from).
    /// Writes go through stored procedures (duplicate checks under one lock, active-parent rules).
    /// </summary>
    public class ProductFunctions : IProductFunctions
    {
        // The three catalog tables are read whole (tens of rows) or by Id; the caller adds WHERE / ORDER BY.
        private const string CategoryColumns = " SELECT Id, Code, Name, Description, SortOrder, IsActive FROM dbo.ProductCategories ";
        private const string ProductColumns = " SELECT Id, CategoryId, Code, Name, Description, SortOrder, IsActive FROM dbo.Products ";
        private const string PlanColumns = " SELECT Id, ProductId, Name, Description, DurationUnit, DurationCount, Price, Cost, SortOrder, IsActive FROM dbo.ProductPlans ";

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly IAuditFunctions _auditFunctions;

        public ProductFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IAuditFunctions __auditFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _auditFunctions = __auditFunctions;
        }

        #region Helpers


        private static ProductCategoryDto MapCategory(DataRow _row) => new ProductCategoryDto
        {
            Id = _row.GetLong("Id"),
            Code = _row.GetText("Code"),
            Name = _row.GetText("Name"),
            Description = _row.GetText("Description"),
            SortOrder = _row.GetInt("SortOrder"),
            IsActive = _row.GetBool("IsActive")
        };

        private static ProductDto MapProduct(DataRow _row) => new ProductDto
        {
            Id = _row.GetLong("Id"),
            CategoryId = _row.GetLong("CategoryId"),
            Code = _row.GetText("Code"),
            Name = _row.GetText("Name"),
            Description = _row.GetText("Description"),
            SortOrder = _row.GetInt("SortOrder"),
            IsActive = _row.GetBool("IsActive")
        };

        private static ProductPlanDto MapPlan(DataRow _row) => new ProductPlanDto
        {
            Id = _row.GetLong("Id"),
            ProductId = _row.GetLong("ProductId"),
            Name = _row.GetText("Name"),
            Description = _row.GetText("Description"),
            DurationUnit = _row.GetText("DurationUnit"),
            DurationCount = _row.GetInt("DurationCount"),
            Price = _row.GetDecimal("Price"),
            Cost = _row.GetDecimal("Cost"),
            SortOrder = _row.GetInt("SortOrder"),
            IsActive = _row.GetBool("IsActive")
        };

        /// <summary>Runs one catalog SELECT and maps its rows.</summary>
        private async Task<List<T>> ReadAsync<T>(string _sql, long? _id, Func<DataRow, T> _map, CancellationToken _ct)
        {
            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sql, _id.HasValue ? new[] { P("@id", _id.Value) } : null, "F:Product:Read", _ct);
            EnsureRead(_functionReturn);
            return _dataTable.MapRows(_map);
        }

        private async Task<ProductCategoryDto> GetCategoryByIdAsync(long _id, CancellationToken _ct)
            => (await ReadAsync(CategoryColumns + " WHERE Id = @id ", _id, MapCategory, _ct)).FirstOrDefault();

        private async Task<ProductDto> GetProductByIdAsync(long _id, CancellationToken _ct)
            => (await ReadAsync(ProductColumns + " WHERE Id = @id ", _id, MapProduct, _ct)).FirstOrDefault();

        private async Task<ProductPlanDto> GetPlanByIdAsync(long _id, CancellationToken _ct)
            => (await ReadAsync(PlanColumns + " WHERE Id = @id ", _id, MapPlan, _ct)).FirstOrDefault();

        // Audit snapshots: business fields only.
        private static object Snapshot(ProductCategoryDto c) => c == null ? null : new { c.Code, c.Name, c.Description, c.SortOrder, c.IsActive };
        private static object Snapshot(ProductDto p) => p == null ? null : new { p.CategoryId, p.Code, p.Name, p.Description, p.SortOrder, p.IsActive };
        private static object Snapshot(ProductPlanDto p) => p == null ? null : new { p.ProductId, p.Name, p.Description, p.DurationUnit, p.DurationCount, p.Price, p.Cost, p.SortOrder, p.IsActive };

        private Task AuditAsync(long? _actorUserId, ApplicationEnumeration.AuditAction _action, ApplicationEnumeration.ObjectType _type, long _id, object _old, object _new, string _ipAddress, CancellationToken _ct)
            => _auditFunctions.LogAsync(_actorUserId, _action.ToString(), _type.ToString(), _id.ToString(), _old, _new, _ipAddress, _ct);

        private static FunctionReturn Ok(string _methodName, string _message, string _httpStatus = Constants.HttpStatusCodes.Ok)
            => new FunctionReturn { MethodName = _methodName, HttpStatusCode = _httpStatus, Message = new List<string> { _message } };

        #endregion

        public async Task<(List<ProductCategoryDto> catalog, IFunctionReturn functionReturn)> GetCatalogAsync(bool _includeInactive, CancellationToken _ct = default)
        {
            string _methodName = "F:Product:GetCatalog";
            try
            {
                // The catalog is small (tens of products): read the three tables and build the tree here.
                List<ProductCategoryDto> _categories = await ReadAsync(CategoryColumns + " ORDER BY SortOrder, Name ", null, MapCategory, _ct);
                List<ProductDto> _products = await ReadAsync(ProductColumns + " ORDER BY SortOrder, Name ", null, MapProduct, _ct);
                List<ProductPlanDto> _plans = await ReadAsync(PlanColumns + " ORDER BY SortOrder, Price, Name ", null, MapPlan, _ct);

                ILookup<long, ProductPlanDto> _plansByProduct = _plans.Where(p => _includeInactive || p.IsActive).ToLookup(p => p.ProductId);
                foreach (ProductDto _product in _products) _product.Plans = _plansByProduct[_product.Id].ToList();
                ILookup<long, ProductDto> _productsByCategory = _products.Where(p => _includeInactive || p.IsActive).ToLookup(p => p.CategoryId);
                foreach (ProductCategoryDto _category in _categories) _category.Products = _productsByCategory[_category.Id].ToList();

                return (_categories.Where(c => _includeInactive || c.IsActive).ToList(), new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(ProductCategoryDto category, IFunctionReturn functionReturn)> CreateCategoryAsync(HttpProductCategoryPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Product:CreateCategory";
            IFunctionReturn _functionReturn;
            long _id;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateProductCategory(_params?.Code, true, _params?.Name, _params?.Description, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                (_functionReturn, _id) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.ProductCategoryCreate, new[]
                {
                    P("@Code", _params.Code.Trim()), P("@Name", Clean(_params.Name)), P("@Description", Clean(_params.Description)), P("@SortOrder", _params.SortOrder), P("@ActorUserId", _actorUserId)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ProductCategoryDto _created = await GetCategoryByIdAsync(_id, _ct);
                await AuditAsync(_actorUserId, ApplicationEnumeration.AuditAction.Create, ApplicationEnumeration.ObjectType.ProductCategory, _id, null, Snapshot(_created), _ipAddress, _ct);
                return (_created, Ok(_methodName, "Category created.", Constants.HttpStatusCodes.Created));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(ProductCategoryDto category, IFunctionReturn functionReturn)> UpdateCategoryAsync(long _id, HttpProductCategoryPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Product:UpdateCategory";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateProductCategory(null, false, _params?.Name, _params?.Description, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ProductCategoryDto _existing = await GetCategoryByIdAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.CategoryNotFound, _methodName));

                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.ProductCategoryUpdate, new[]
                {
                    P("@Id", _id), P("@Name", Clean(_params.Name)), P("@Description", Clean(_params.Description)), P("@SortOrder", _params.SortOrder),
                    P("@IsActive", _params.IsActive), P("@ActorUserId", _actorUserId)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ProductCategoryDto _updated = await GetCategoryByIdAsync(_id, _ct);
                await AuditAsync(_actorUserId, _existing.IsActive != _updated.IsActive ? ApplicationEnumeration.AuditAction.StatusChange : ApplicationEnumeration.AuditAction.Update,
                    ApplicationEnumeration.ObjectType.ProductCategory, _id, Snapshot(_existing), Snapshot(_updated), _ipAddress, _ct);
                return (_updated, Ok(_methodName, "Category updated."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(ProductDto product, IFunctionReturn functionReturn)> CreateProductAsync(HttpProductPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Product:CreateProduct";
            IFunctionReturn _functionReturn;
            long _id;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateProduct(_params?.CategoryId ?? 0, _params?.Code, true, _params?.Name, _params?.Description, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                // The procedure checks the category (exists, active) and duplicates under one lock.
                (_functionReturn, _id) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.ProductCreate, new[]
                {
                    P("@CategoryId", _params.CategoryId), P("@Code", _params.Code.Trim()), P("@Name", Clean(_params.Name)), P("@Description", Clean(_params.Description)),
                    P("@SortOrder", _params.SortOrder), P("@ActorUserId", _actorUserId)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ProductDto _created = await GetProductByIdAsync(_id, _ct);
                await AuditAsync(_actorUserId, ApplicationEnumeration.AuditAction.Create, ApplicationEnumeration.ObjectType.Product, _id, null, Snapshot(_created), _ipAddress, _ct);
                return (_created, Ok(_methodName, "Product created.", Constants.HttpStatusCodes.Created));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(ProductDto product, IFunctionReturn functionReturn)> UpdateProductAsync(long _id, HttpProductPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Product:UpdateProduct";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateProduct(_params?.CategoryId ?? 0, null, false, _params?.Name, _params?.Description, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ProductDto _existing = await GetProductByIdAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.ProductNotFound, _methodName));

                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.ProductUpdate, new[]
                {
                    P("@Id", _id), P("@CategoryId", _params.CategoryId), P("@Name", Clean(_params.Name)), P("@Description", Clean(_params.Description)),
                    P("@SortOrder", _params.SortOrder), P("@IsActive", _params.IsActive), P("@ActorUserId", _actorUserId)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ProductDto _updated = await GetProductByIdAsync(_id, _ct);
                await AuditAsync(_actorUserId, _existing.IsActive != _updated.IsActive ? ApplicationEnumeration.AuditAction.StatusChange : ApplicationEnumeration.AuditAction.Update,
                    ApplicationEnumeration.ObjectType.Product, _id, Snapshot(_existing), Snapshot(_updated), _ipAddress, _ct);
                return (_updated, Ok(_methodName, "Product updated."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(ProductPlanDto plan, IFunctionReturn functionReturn)> CreatePlanAsync(long _productId, HttpProductPlanPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Product:CreatePlan";
            IFunctionReturn _functionReturn;
            long _id;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateProductPlan(_params?.Name, _params?.Description, _params?.DurationUnit, _params?.DurationCount ?? 0, _params?.Price, _params?.Cost, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                (_functionReturn, _id) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.ProductPlanCreate, new[]
                {
                    P("@ProductId", _productId), P("@Name", Clean(_params.Name)), P("@Description", Clean(_params.Description)), P("@DurationUnit", _params.DurationUnit),
                    P("@DurationCount", _params.DurationCount), Money("@Price", _params.Price.Value), Money("@Cost", _params.Cost.Value), P("@SortOrder", _params.SortOrder), P("@ActorUserId", _actorUserId)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ProductPlanDto _created = await GetPlanByIdAsync(_id, _ct);
                await AuditAsync(_actorUserId, ApplicationEnumeration.AuditAction.Create, ApplicationEnumeration.ObjectType.ProductPlan, _id, null, Snapshot(_created), _ipAddress, _ct);
                return (_created, Ok(_methodName, "Plan created.", Constants.HttpStatusCodes.Created));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(ProductPlanDto plan, IFunctionReturn functionReturn)> UpdatePlanAsync(long _id, HttpProductPlanPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Product:UpdatePlan";
            IFunctionReturn _functionReturn;
            #endregion
            try
            {
                _functionReturn = ApplicationValidationFunctions.ValidateProductPlan(_params?.Name, _params?.Description, _params?.DurationUnit, _params?.DurationCount ?? 0, _params?.Price, _params?.Cost, _methodName);
                if (!_functionReturn.Status) return (null, _functionReturn);

                ProductPlanDto _existing = await GetPlanByIdAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(ApplicationConstants.ValidationMessages.PlanNotFound, _methodName));

                (_functionReturn, _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.ProductPlanUpdate, new[]
                {
                    P("@Id", _id), P("@Name", Clean(_params.Name)), P("@Description", Clean(_params.Description)), P("@DurationUnit", _params.DurationUnit),
                    P("@DurationCount", _params.DurationCount), Money("@Price", _params.Price.Value), Money("@Cost", _params.Cost.Value),
                    P("@SortOrder", _params.SortOrder), P("@IsActive", _params.IsActive), P("@ActorUserId", _actorUserId)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                // Price-list changes are audited with the old and new price and cost.
                ProductPlanDto _updated = await GetPlanByIdAsync(_id, _ct);
                await AuditAsync(_actorUserId, _existing.IsActive != _updated.IsActive ? ApplicationEnumeration.AuditAction.StatusChange : ApplicationEnumeration.AuditAction.Update,
                    ApplicationEnumeration.ObjectType.ProductPlan, _id, Snapshot(_existing), Snapshot(_updated), _ipAddress, _ct);
                return (_updated, Ok(_methodName, "Plan updated."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
