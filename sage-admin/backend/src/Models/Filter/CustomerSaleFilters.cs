using System;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.Models.Filter
{
    /// <summary>Query string of GET api/admin/customers.</summary>
    public class CustomerFilter : HttpPaging
    {
        public string Search { get; set; }
    }

    /// <summary>Query string of GET api/admin/sales.</summary>
    public class AdminSaleFilter : HttpPaging
    {
        /// <summary>Sales made by this sales person (or manager).</summary>
        public Guid? SellerId { get; set; }
        /// <summary>Manager-wise: this manager's own sales plus their team's.</summary>
        public Guid? ManagerId { get; set; }
        public Guid? CustomerId { get; set; }
        public string Search { get; set; }
        /// <summary>Active or Cancelled; anything else means both.</summary>
        public string Status { get; set; }
        public DateOnly? SoldFrom { get; set; }
        public DateOnly? SoldTo { get; set; }
    }

    /// <summary>Query string of GET api/admin/sales/summary.</summary>
    public class SalesSummaryFilter
    {
        /// <summary>"seller" (default) or "manager".</summary>
        public string GroupBy { get; set; }
        public DateOnly? SoldFrom { get; set; }
        public DateOnly? SoldTo { get; set; }
    }
}
