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
    /// <summary>User Panel accounts (sales persons / sales managers), managed from the Admin Panel.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/sales-users")]
    [ApiController]
    public class SalesUsersController : AdminControllerBase
    {
        private readonly ISalesUserFunctions _salesUserFunctions;

        public SalesUsersController(ILoggerFunctions __loggerFunctions, ISalesUserFunctions __salesUserFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _salesUserFunctions = __salesUserFunctions;
        }

        /// <summary>Paged list: search, role, status, managerId (a manager's team), sort.</summary>
        [HttpGet]
        [RequirePermission(PermissionConstants.SalesUserView)]
        public Task<IActionResult> GetSalesUsers([FromQuery] SalesUserFilter _filter, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: SalesUser.View
            //*************************************************************
            return RunAsync<PagedResult<SalesUserDto>>("C:SalesUsers:GetSalesUsers", () => _salesUserFunctions.GetSalesUsersAsync(_filter, _ct));
        }

        /// <summary>Active sales managers, for the manager drop-down.</summary>
        [HttpGet]
        [Route("managers")]
        [RequirePermission(PermissionConstants.SalesUserView)]
        public Task<IActionResult> GetManagers(CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: SalesUser.View
            //*************************************************************
            return RunAsync<List<SalesManagerOption>>("C:SalesUsers:GetManagers", () => _salesUserFunctions.GetManagersAsync(_ct));
        }

        [HttpGet]
        [Route("{id:guid}")]
        [RequirePermission(PermissionConstants.SalesUserView)]
        public Task<IActionResult> GetSalesUser(Guid id, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: SalesUser.View
            //*************************************************************
            return RunAsync<SalesUserDto>("C:SalesUsers:GetSalesUser", () => _salesUserFunctions.GetSalesUserAsync(id, _ct));
        }

        /// <summary>Create a sales person (needs a manager) or a sales manager, with a temporary password.</summary>
        [HttpPost]
        [RequirePermission(PermissionConstants.SalesUserAdd)]
        public Task<IActionResult> CreateSalesUser([FromBody] HttpSalesUserPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: SalesUser.Add
            //*************************************************************
            return RunAsync<SalesUserDto>("C:SalesUsers:CreateSalesUser", async () => await _salesUserFunctions.CreateSalesUserAsync(_params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Edit details, role and manager. The username never changes.</summary>
        [HttpPut]
        [Route("{id:guid}")]
        [RequirePermission(PermissionConstants.SalesUserEdit)]
        public Task<IActionResult> UpdateSalesUser(Guid id, [FromBody] HttpSalesUserPutParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: SalesUser.Edit
            //*************************************************************
            return RunAsync<SalesUserDto>("C:SalesUsers:UpdateSalesUser", async () => await _salesUserFunctions.UpdateSalesUserAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Activate (also clears a lockout) or deactivate.</summary>
        [HttpPut]
        [Route("{id:guid}/status")]
        [RequirePermission(PermissionConstants.SalesUserChangeStatus)]
        public Task<IActionResult> SetStatus(Guid id, [FromBody] HttpSalesUserStatusParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: SalesUser.ChangeStatus
            //*************************************************************
            return RunAsync<SalesUserDto>("C:SalesUsers:SetStatus", async () => await _salesUserFunctions.SetStatusAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct));
        }

        /// <summary>Set a temporary password; the user must change it at next sign-in.</summary>
        [HttpPost]
        [Route("{id:guid}/reset-password")]
        [RequirePermission(PermissionConstants.SalesUserResetPassword)]
        public Task<IActionResult> ResetPassword(Guid id, [FromBody] HttpSalesUserPasswordParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: SalesUser.ResetPassword
            //*************************************************************
            return RunAsync<object>("C:SalesUsers:ResetPassword", async () =>
            {
                IFunctionReturn _functionReturn = await _salesUserFunctions.ResetPasswordAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct);
                return (null, _functionReturn);
            });
        }
    }
}
