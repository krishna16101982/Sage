using SageCommonLib.Models.Paging;

namespace SageAdminApi.Models.Filter
{
    /// <summary>Query-string filter for GET api/admin/permissions (paging and sorting come from HttpPaging).</summary>
    public class PermissionFilter : HttpPaging
    {
        public long? ModuleId { get; set; }
        public string Search { get; set; }
        public bool? IsActive { get; set; }
    }
}
