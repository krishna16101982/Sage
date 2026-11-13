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
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.Controllers
{
    [Route(ApplicationConstants.Api.RoutePrefix + "/permissions")]
    [ApiController]
    public class PermissionsController : AdminControllerBase
    {
        private readonly IPermissionFunctions _permissionFunctions;

        public PermissionsController(ILoggerFunctions __loggerFunctions, IPermissionFunctions __permissionFunctions, IAuthorizationFunctions __authorizationFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse, __authorizationFunctions)
        {
            _permissionFunctions = __permissionFunctions;
        }

        private IActionResult Created<T>(T _data, string _message)
            => new ObjectResult(_apiResponse.Success(_data, _message, Constants.HttpStatusCodes.Created)) { StatusCode = 201 };

        /// <summary>Paged, filterable list of permissions (module, search, active flag, sort).</summary>
        [HttpGet]
        [RequirePermission(PermissionConstants.PermissionView)]
        public async Task<IActionResult> GetPermissions([FromQuery] PermissionFilter _filter, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Permission.View
            //*************************************************************
            string _methodName = "C:Permissions:GetPermissions";
            try
            {
                (PagedResult<PermissionDto> _result, IFunctionReturn _functionReturn) = await _permissionFunctions.GetPermissionsAsync(_filter, _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return new OkObjectResult(_apiResponse.Success(_result));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Modules with their permissions (used by the group permission matrix). includeInactive=true for management screens.</summary>
        [HttpGet]
        [Route("modules")]
        [RequirePermission(PermissionConstants.PermissionView)]
        public async Task<IActionResult> GetModules([FromQuery] bool includeInactive, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Permission.View
            //*************************************************************
            string _methodName = "C:Permissions:GetModules";
            try
            {
                (List<ModuleDto> _modules, IFunctionReturn _functionReturn) = await _permissionFunctions.GetModulesAsync(includeInactive, _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return new OkObjectResult(_apiResponse.Success(_modules));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Create a module.</summary>
        [HttpPost]
        [Route("modules")]
        [RequirePermission(PermissionConstants.PermissionAdd)]
        public async Task<IActionResult> CreateModule([FromBody] HttpModulePostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Permission.Add
            //*************************************************************
            string _methodName = "C:Permissions:CreateModule";
            try
            {
                (ModuleDto _module, IFunctionReturn _functionReturn) = await _permissionFunctions.CreateModuleAsync(_params, await ActorIdAsync(_ct), ApplicationCommonFunctions.GetClientIp(HttpContext), _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return Created(_module, "Module created.");
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Edit a module (name, category, order, active flag). The code never changes.</summary>
        [HttpPut]
        [Route("modules/{id:long}")]
        [RequirePermission(PermissionConstants.PermissionEdit)]
        public async Task<IActionResult> UpdateModule(long id, [FromBody] HttpModulePutParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Permission.Edit
            //*************************************************************
            string _methodName = "C:Permissions:UpdateModule";
            try
            {
                (ModuleDto _module, IFunctionReturn _functionReturn) = await _permissionFunctions.UpdateModuleAsync(id, _params, await ActorIdAsync(_ct), ApplicationCommonFunctions.GetClientIp(HttpContext), _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return new OkObjectResult(_apiResponse.Success(_module, "Module updated."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Create a permission. Its code is Module.Action.</summary>
        [HttpPost]
        [RequirePermission(PermissionConstants.PermissionAdd)]
        public async Task<IActionResult> CreatePermission([FromBody] HttpPermissionPostParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Permission.Add
            //*************************************************************
            string _methodName = "C:Permissions:CreatePermission";
            try
            {
                (PermissionDto _permission, IFunctionReturn _functionReturn) = await _permissionFunctions.CreatePermissionAsync(_params, await ActorIdAsync(_ct), ApplicationCommonFunctions.GetClientIp(HttpContext), _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return Created(_permission, "Permission created.");
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }

        /// <summary>Edit a permission (name, description, active flag). Module and action never change.</summary>
        [HttpPut]
        [Route("{id:long}")]
        [RequirePermission(PermissionConstants.PermissionEdit)]
        public async Task<IActionResult> UpdatePermission(long id, [FromBody] HttpPermissionPutParams _params, CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: Permission.Edit
            //*************************************************************
            string _methodName = "C:Permissions:UpdatePermission";
            try
            {
                (PermissionDto _permission, IFunctionReturn _functionReturn) = await _permissionFunctions.UpdatePermissionAsync(id, _params, await ActorIdAsync(_ct), ApplicationCommonFunctions.GetClientIp(HttpContext), _ct);
                if (!_functionReturn.Status) return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                return new OkObjectResult(_apiResponse.Success(_permission, "Permission updated."));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }
    }
}
