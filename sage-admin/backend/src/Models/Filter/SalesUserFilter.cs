using System;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.Models.Filter
{
    /// <summary>Query-string filter for GET api/admin/sales-users (paging and sorting come from HttpPaging).</summary>
    public class SalesUserFilter : HttpPaging
    {
        public string Search { get; set; }
        /// <summary>SalesPerson or SalesManager; anything else means no filter.</summary>
        public string Role { get; set; }
        /// <summary>Active, Inactive or Locked; anything else means no filter.</summary>
        public string Status { get; set; }
        /// <summary>Only this manager's team.</summary>
        public Guid? ManagerId { get; set; }
    }
}
