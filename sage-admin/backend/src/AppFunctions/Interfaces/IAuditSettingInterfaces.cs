using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SageAdminApi.Models;
using SageAdminApi.Models.Filter;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.AppFunctions.Interfaces
{
    /// <summary>Reading the audit log (writing is IAuditFunctions).</summary>
    public interface IAuditLogFunctions
    {
        Task<(PagedResult<AuditLogItem> result, IFunctionReturn functionReturn)> GetAuditLogsAsync(AuditLogFilter _filter, CancellationToken _ct = default);
        Task<(AuditLogDetail entry, IFunctionReturn functionReturn)> GetAuditLogAsync(long _id, CancellationToken _ct = default);
        Task<(AuditLogOptions options, IFunctionReturn functionReturn)> GetOptionsAsync(CancellationToken _ct = default);
    }

    public interface ISettingFunctions
    {
        Task<(List<SettingDto> settings, IFunctionReturn functionReturn)> GetSettingsAsync(CancellationToken _ct = default);
        Task<(SettingDto setting, IFunctionReturn functionReturn)> UpdateSettingAsync(string _key, HttpSettingPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
    }
}
