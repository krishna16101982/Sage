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
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.Controllers
{
    /// <summary>The audit log (read-only): who changed what, when, from where, with the values before and after.</summary>
    [Route(ApplicationConstants.Api.RoutePrefix + "/audit-logs")]
    [ApiController]
    public class AuditLogsController : AdminControllerBase
    {
        private readonly IAuditLogFunctions _auditLogFunctions;

        public AuditLogsController(ILoggerFunctions __loggerFunctions, IAuditLogFunctions __auditLogFunctions, IApiResponse __apiResponse)
            : base(__loggerFunctions, __apiResponse)
        {
            _auditLogFunctions = __auditLogFunctions;
        }

        /// <summary>Newest first. Filters: search, entityType, action, actorType (admin|sales|system), actorId, from, to; paging.</summary>
        [HttpGet]
        [RequirePermission(PermissionConstants.AuditLogView)]
        public Task<IActionResult> GetAuditLogs([FromQuery] AuditLogFilter _filter, CancellationToken _ct)
            => RunAsync<PagedResult<AuditLogItem>>("C:AuditLogs:GetAuditLogs", () => _auditLogFunctions.GetAuditLogsAsync(_filter, _ct));

        /// <summary>Entity types, actions and admin users that appear in the log, for the filters.</summary>
        [HttpGet("options")]
        [RequirePermission(PermissionConstants.AuditLogView)]
        public Task<IActionResult> GetOptions(CancellationToken _ct)
            => RunAsync<AuditLogOptions>("C:AuditLogs:GetOptions", () => _auditLogFunctions.GetOptionsAsync(_ct));

        /// <summary>One entry with its old and new values.</summary>
        [HttpGet("{id:long}")]
        [RequirePermission(PermissionConstants.AuditLogView)]
        public Task<IActionResult> GetAuditLog(long id, CancellationToken _ct)
            => RunAsync<AuditLogDetail>("C:AuditLogs:GetAuditLog", () => _auditLogFunctions.GetAuditLogAsync(id, _ct));
    }
}
