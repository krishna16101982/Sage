using System;
using SageCommonLib.Models.Paging;

namespace SageUserApi.Models.HttpPost
{
    /// <summary>Body of POST api/user/customers.</summary>
    public class HttpCustomerPostParams
    {
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Gstin { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
    }

    /// <summary>
    /// Body of POST api/user/sales. Price and cost are NOT accepted: the server takes them from the price list.
    /// Give either SaleAmount or DiscountAmount; the other is calculated. Duration and renewal date default from the plan.
    /// </summary>
    public class HttpSalePostParams
    {
        public Guid? CustomerId { get; set; }
        public long PlanId { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal? SaleAmount { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string DurationUnit { get; set; }
        public int? DurationCount { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? RenewalDate { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>Body of POST api/user/sales/{id}/cancel (managers) and POST api/user/sales/{id}/cancel-request (sales persons).</summary>
    public class HttpSaleCancelParams
    {
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>Body of POST api/user/sales/cancel-requests/{id}/approve and /reject (optional note to the sales person).</summary>
    public class HttpCancelDecisionParams
    {
        public string Note { get; set; }
    }

    /// <summary>Body of PUT api/user/sales/team-members/{id}/cancel-permission.</summary>
    public class HttpCancelPermissionParams
    {
        public bool CanRequestCancellation { get; set; }
    }
}

namespace SageUserApi.Models.Filter
{
    /// <summary>Query string of GET api/user/customers.</summary>
    public class CustomerFilter : HttpPaging
    {
        public string Search { get; set; }
    }

    /// <summary>Query string of GET api/user/sales.</summary>
    public class SaleFilter : HttpPaging
    {
        /// <summary>"mine" (default) or "team" (sales managers only).</summary>
        public string Scope { get; set; }
        /// <summary>Team scope only: one team member's sales.</summary>
        public Guid? SellerId { get; set; }
        public string Search { get; set; }
        /// <summary>Active or Cancelled; anything else means both.</summary>
        public string Status { get; set; }
        public DateOnly? SoldFrom { get; set; }
        public DateOnly? SoldTo { get; set; }
    }
}
