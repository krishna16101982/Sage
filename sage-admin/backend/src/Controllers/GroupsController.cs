using System;
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
using SageCommonLib.Globals;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.Controllers
{
    [Route(ApplicationConstants.Api.RoutePrefix + "/groups")]
    [ApiController]
    public class GroupsController : AdminControllerBase
    {
        private readonly IGroupFunctions _groupFunctions;

        public GroupsController(ILoggerFunctions __loggerFunctions, IGroupFunctions __groupFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _groupFunctions = __groupFunctions;
        }

        /// <summary>Paged, filterable list of groups (search, status, sort) with member and permission counts.</summary>
        [HttpGet]
        [RequirePermission(PermissionConstants.GroupView)]
        public async Task<IActionResult> GetGroups([FromQuery] GroupFilter _filter, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Group.View
            //*************************************************************
            string _methodName = "C:Groups:GetGroups";
            try
            {
                (PagedResult<GroupDto> _result, IFunctionReturn _functionReturn) = await _groupFunctions.GetGroupsAsync(_filter, _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return new OkObjectResult(_apiResponse.Success(_result));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>One group with its permission matrix (modules, permissions, assigned flags).</summary>
        [HttpGet]
        [Route("{id:guid}")]
        [RequirePermission(PermissionConstants.GroupView)]
        public async Task<IActionResult> GetGroup(Guid id, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Group.View
            //*************************************************************
            string _methodName = "C:Groups:GetGroup";
            try
            {
                (GroupDetailDto _group, IFunctionReturn _functionReturn) = await _groupFunctions.GetGroupAsync(id, _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return new OkObjectResult(_apiResponse.Success(_group));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Create a group (active, no permissions yet).</summary>
        [HttpPost]
        [RequirePermission(PermissionConstants.GroupAdd)]
        public async Task<IActionResult> CreateGroup([FromBody] HttpGroupPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Group.Add
            //*************************************************************
            string _methodName = "C:Groups:CreateGroup";
            try
            {
                (GroupDto _group, IFunctionReturn _functionReturn) = await _groupFunctions.CreateGroupAsync(_params, await ActorIdAsync(_ct), ClientIp, _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return new ObjectResult(_apiResponse.Success(_group, "Group created.", Constants.HttpStatusCodes.Created)) { StatusCode = 201 };
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Edit a group (name, description, status). The code never changes.</summary>
        [HttpPut]
        [Route("{id:guid}")]
        [RequirePermission(PermissionConstants.GroupEdit)]
        public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] HttpGroupPutParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Group.Edit
            //*************************************************************
            string _methodName = "C:Groups:UpdateGroup";
            try
            {
                (GroupDto _group, IFunctionReturn _functionReturn) = await _groupFunctions.UpdateGroupAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return new OkObjectResult(_apiResponse.Success(_group, "Group updated."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Delete a group. Refused with 409 while users are assigned to it.</summary>
        [HttpDelete]
        [Route("{id:guid}")]
        [RequirePermission(PermissionConstants.GroupDelete)]
        public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Group.Delete
            //*************************************************************
            string _methodName = "C:Groups:DeleteGroup";
            try
            {
                IFunctionReturn _functionReturn = await _groupFunctions.DeleteGroupAsync(id, await ActorIdAsync(_ct), ClientIp, _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return new OkObjectResult(_apiResponse.Success<object>(null, "Group deleted."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Replace the group's permission set. Members' access changes on their next request.</summary>
        [HttpPut]
        [Route("{id:guid}/permissions")]
        [RequirePermission(PermissionConstants.GroupAssignPermissions)]
        public async Task<IActionResult> SetPermissions(Guid id, [FromBody] HttpGroupPermissionsPutParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Group.AssignPermissions
            //*************************************************************
            string _methodName = "C:Groups:SetPermissions";
            try
            {
                (GroupDetailDto _group, IFunctionReturn _functionReturn) = await _groupFunctions.SetGroupPermissionsAsync(id, _params, await ActorIdAsync(_ct), ClientIp, _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return new OkObjectResult(_apiResponse.Success(_group, "Group permissions saved."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }
    }
}
