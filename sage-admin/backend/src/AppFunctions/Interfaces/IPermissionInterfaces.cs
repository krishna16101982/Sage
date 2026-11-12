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

    public interface IAuthorizationFunctions
    {
        /// <summary>True when the user is an active Super Admin or holds the permission through an active group (always read from SQL).</summary>
        Task<bool> HasPermissionAsync(Guid _userPublicId, string _permissionCode, CancellationToken _ct = default);
        /// <summary>Internal user id for audit trails (null when unknown).</summary>
        Task<long?> GetUserIdAsync(Guid _userPublicId, CancellationToken _ct = default);
    }
}
