using SageCommonLib.Models.Paging;

namespace SageAdminApi.Models.Filter
{
    /// <summary>Query-string filter for GET api/admin/groups (paging and sorting come from HttpPaging).</summary>
    public class GroupFilter : HttpPaging
    {
        public string Search { get; set; }
        /// <summary>Active or Inactive; anything else means no filter.</summary>
        public string Status { get; set; }
    }
}
