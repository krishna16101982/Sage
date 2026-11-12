using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SageAdminApi.Models;
using SageAdminApi.Models.Filter;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.AppFunctions.Interfaces
{
    public interface IPermissionFunctions
    {
        Task<(PagedResult<PermissionDto> result, IFunctionReturn functionReturn)> GetPermissionsAsync(PermissionFilter _filter, CancellationToken _ct = default);
        Task<(List<ModuleDto> modules, IFunctionReturn functionReturn)> GetModulesAsync(bool _includeInactive, CancellationToken _ct = default);
        Task<(ModuleDto module, IFunctionReturn functionReturn)> CreateModuleAsync(HttpModulePostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(ModuleDto module, IFunctionReturn functionReturn)> UpdateModuleAsync(long _id, HttpModulePutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(PermissionDto permission, IFunctionReturn functionReturn)> CreatePermissionAsync(HttpPermissionPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(PermissionDto permission, IFunctionReturn functionReturn)> UpdatePermissionAsync(long _id, HttpPermissionPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
    }

    public interface IGroupFunctions
    {
        Task<(PagedResult<GroupDto> result, IFunctionReturn functionReturn)> GetGroupsAsync(GroupFilter _filter, CancellationToken _ct = default);
        Task<(GroupDetailDto group, IFunctionReturn functionReturn)> GetGroupAsync(Guid _id, CancellationToken _ct = default);
        Task<(GroupDto group, IFunctionReturn functionReturn)> CreateGroupAsync(HttpGroupPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(GroupDto group, IFunctionReturn functionReturn)> UpdateGroupAsync(Guid _id, HttpGroupPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<IFunctionReturn> DeleteGroupAsync(Guid _id, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(GroupDetailDto group, IFunctionReturn functionReturn)> SetGroupPermissionsAsync(Guid _id, HttpGroupPermissionsPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
    }

    public interface IProductFunctions
    {
        /// <summary>Categories -> products -> plans. includeInactive=false returns only what can be sold.</summary>
        Task<(List<ProductCategoryDto> catalog, IFunctionReturn functionReturn)> GetCatalogAsync(bool _includeInactive, CancellationToken _ct = default);
        Task<(ProductCategoryDto category, IFunctionReturn functionReturn)> CreateCategoryAsync(HttpProductCategoryPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(ProductCategoryDto category, IFunctionReturn functionReturn)> UpdateCategoryAsync(long _id, HttpProductCategoryPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(ProductDto product, IFunctionReturn functionReturn)> CreateProductAsync(HttpProductPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(ProductDto product, IFunctionReturn functionReturn)> UpdateProductAsync(long _id, HttpProductPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(ProductPlanDto plan, IFunctionReturn functionReturn)> CreatePlanAsync(long _productId, HttpProductPlanPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(ProductPlanDto plan, IFunctionReturn functionReturn)> UpdatePlanAsync(long _id, HttpProductPlanPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
    }

    public interface IAuthorizationFunctions
    {
        /// <summary>True when the user is an active Super Admin or holds the permission through an active group (always read from SQL).</summary>
        Task<bool> HasPermissionAsync(Guid _userPublicId, string _permissionCode, CancellationToken _ct = default);
        /// <summary>Internal user id for audit trails (null when unknown).</summary>
        Task<long?> GetUserIdAsync(Guid _userPublicId, CancellationToken _ct = default);
    }
}
