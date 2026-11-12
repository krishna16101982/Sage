using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.Controllers
{
    [Route(ApplicationConstants.Api.RoutePrefix + "/[controller]")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly IStatusFunctions _statusFunctions;
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IApiResponse _apiResponse;

        public StatusController(ILoggerFunctions __loggerFunctions, IStatusFunctions __statusFunctions, IApiResponse __apiResponse)
        {
            _loggerFunctions = __loggerFunctions;
            _statusFunctions = __statusFunctions;
            _apiResponse = __apiResponse;
        }

        /// <summary>
        /// Liveness plus database ping. GIT_PROJECT_NAME / GIT_BUILD_TAG are replaced by the Docker build (see Dockerfile.*).
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetStatus(CancellationToken _ct)
        {
            //*************************************************************
            // Required Permission: AllowAnonymous
            //*************************************************************
            string _methodName = "C:Status:GetStatus";
            try
            {
                (ServiceStatus _serviceStatus, IFunctionReturn _functionReturn) = await _statusFunctions.GetStatusAsync("GIT_PROJECT_NAME", "GIT_BUILD_TAG", _ct);

                if (!_functionReturn.Status)
                {
                    _loggerFunctions.Warning(_methodName, string.Join("; ", _functionReturn.Message));
                    return ApplicationCommonFunctions.ErrorResult(_apiResponse, _functionReturn);
                }
                return new OkObjectResult(_apiResponse.Success(_serviceStatus));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return ApplicationCommonFunctions.SystemErrorResult(_apiResponse);
            }
        }
    }
}
