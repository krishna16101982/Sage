using System;
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

    /// <summary>Order form lists (dbo.MasterValues).</summary>
    /// <summary>Lead lists of the User Panel's lead form: products, lead sources, billing cycles (one table each).</summary>
    public interface ILeadListFunctions
    {
        Task<(List<LeadList> lists, IFunctionReturn functionReturn)> GetListsAsync(CancellationToken _ct = default);
        /// <summary>Adds (_id null) or changes one value of the list named in _params.</summary>
        Task<(LeadListValueDto value, IFunctionReturn functionReturn)> SaveAsync(int? _id, HttpLeadListValuePostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
    }

    /// <summary>The Admin dashboard's lead figures.</summary>
    public interface ILeadSummaryFunctions
    {
        /// <summary>Every lead (_salesUserId null), or the leads one sales user created, generated or is assigned to.</summary>
        Task<(LeadSummary summary, IFunctionReturn functionReturn)> GetSummaryAsync(Guid? _salesUserId, CancellationToken _ct = default);
    }

    public interface IMasterValueFunctions
    {
        Task<(List<MasterList> lists, IFunctionReturn functionReturn)> GetListsAsync(CancellationToken _ct = default);
        /// <summary>Adds (_id null) or changes one value.</summary>
        Task<(MasterValueDto value, IFunctionReturn functionReturn)> SaveAsync(int? _id, HttpMasterValuePostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
    }

    public interface ISettingFunctions
    {
        Task<(List<SettingDto> settings, IFunctionReturn functionReturn)> GetSettingsAsync(CancellationToken _ct = default);
        Task<(SettingDto setting, IFunctionReturn functionReturn)> UpdateSettingAsync(string _key, HttpSettingPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
    }
}
