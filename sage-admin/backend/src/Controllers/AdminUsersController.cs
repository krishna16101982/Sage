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
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.Controllers
{
    /// <summary>Admin Panel accounts.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/users")]
    [ApiController]
    public class AdminUsersController : AdminControllerBase
    {
        private readonly IAdminUserFunctions _adminUserFunctions;

        public AdminUsersController(ILoggerFunctions __loggerFunctions, IAdminUserFunctions __adminUserFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _adminUserFunctions = __adminUserFunctions;
        }

        /// <summary>Paged list: search, status, groupId, sort.</summary>
        [HttpGet]
        [RequirePermission(PermissionConstants.AdminUserView)]
        public Task<IActionResult> GetAdminUsers([FromQuery] AdminUserFilter _filter, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AdminUser.View
            //*************************************************************
            return RunAsync<PagedResult<AdminUserDto>>("C:AdminUsers:GetAdminUsers", () => _adminUserFunctions.GetAdminUsersAsync(_filter, _ct));
        }

        /// <summary>Active permission groups, for the group drop-down (does not need Group.View).</summary>
        [HttpGet]
        [Route("groups")]
        [RequirePermission(PermissionConstants.AdminUserView)]
        public Task<IActionResult> GetGroupOptions(CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AdminUser.View
            //*************************************************************
            return RunAsync<List<GroupOption>>("C:AdminUsers:GetGroupOptions", () => _adminUserFunctions.GetGroupOptionsAsync(_ct));
        }

        [HttpGet]
        [Route("{id:guid}")]
        [RequirePermission(PermissionConstants.AdminUserView)]
        public Task<IActionResult> GetAdminUser(Guid id, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AdminUser.View
            //*************************************************************
            return RunAsync<AdminUserDto>("C:AdminUsers:GetAdminUser", () => _adminUserFunctions.GetAdminUserAsync(id, _ct));
        }

        /// <summary>Create an admin user with a group and a temporary password.</summary>
        [HttpPost]
        [RequirePermission(PermissionConstants.AdminUserAdd)]
        public Task<IActionResult> CreateAdminUser([FromBody] HttpAdminUserPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AdminUser.Add
            //*************************************************************
            return RunAsync<AdminUserDto>("C:AdminUsers:CreateAdminUser", async () => await _adminUserFunctions.CreateAdminUserAsync(_params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Edit details and group. The username never changes.</summary>
        [HttpPut]
        [Route("{id:guid}")]
        [RequirePermission(PermissionConstants.AdminUserEdit)]
        public Task<IActionResult> UpdateAdminUser(Guid id, [FromBody] HttpAdminUserPutParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AdminUser.Edit
            //*************************************************************
            return RunAsync<AdminUserDto>("C:AdminUsers:UpdateAdminUser", async () => await _adminUserFunctions.UpdateAdminUserAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Activate (also clears a lockout) or deactivate (ends their sessions).</summary>
        [HttpPut]
        [Route("{id:guid}/status")]
        [RequirePermission(PermissionConstants.AdminUserChangeStatus)]
        public Task<IActionResult> SetStatus(Guid id, [FromBody] HttpUserStatusParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AdminUser.ChangeStatus
            //*************************************************************
            return RunAsync<AdminUserDto>("C:AdminUsers:SetStatus", async () => await _adminUserFunctions.SetStatusAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Set a temporary password for another admin user; they must change it at next sign-in.</summary>
        [HttpPost]
        [Route("{id:guid}/reset-password")]
        [RequirePermission(PermissionConstants.AdminUserResetPassword)]
        public Task<IActionResult> ResetPassword(Guid id, [FromBody] HttpSalesUserPasswordParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AdminUser.ResetPassword
            //*************************************************************
            return RunAsync<object>("C:AdminUsers:ResetPassword", async () =>
            {
                IFunctionReturn _functionReturn = await _adminUserFunctions.ResetPasswordAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct);
                return (null, _functionReturn);
            });
        }
    }
}
