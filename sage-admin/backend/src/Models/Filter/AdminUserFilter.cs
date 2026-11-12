using System;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.Models.Filter
{
    /// <summary>Query-string filter for GET api/admin/users (paging and sorting come from HttpPaging).</summary>
    public class AdminUserFilter : HttpPaging
    {
        public string Search { get; set; }
        /// <summary>Active, Inactive or Locked; anything else means no filter.</summary>
        public string Status { get; set; }
        /// <summary>Only members of this group.</summary>
        public Guid? GroupId { get; set; }
    }
}
