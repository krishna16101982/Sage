using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageUserApi.Globals;

namespace SageUserApi.Controllers
{
    /// <summary>Health check for monitoring and deployment: is the API up and can it reach the database?</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/status")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly IApiResponse _apiResponse;

        public StatusController(IDatabaseFunctions __databaseFunctions, IApiResponse __apiResponse)
        {
            _databaseFunctions = __databaseFunctions;
            _apiResponse = __apiResponse;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Get(CancellationToken _ct)
        {
            bool _dbOk = await _databaseFunctions.PingAsync(_ct);
            var _body = new { service = "SageUserApi", database = _dbOk ? "Up" : "Down", timeUtc = DateTime.UtcNow };
            return _dbOk ? Ok(_apiResponse.Success(_body)) : StatusCode(503, _apiResponse.Success(_body, "Database unavailable", "503"));
        }
    }
}
