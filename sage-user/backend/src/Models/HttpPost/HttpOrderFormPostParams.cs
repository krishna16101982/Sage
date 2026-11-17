using System;
using SageUserApi.Models;

namespace SageUserApi.Models.HttpPost
{
    /// <summary>Body of POST api/user/order-form: everything the simple sale has, plus the order-form details.</summary>
    public class HttpOrderFormPostParams
    {
        // ---- The sale (same rules as POST api/user/sales) ----
        public Guid? CustomerId { get; set; }
        public long PlanId { get; set; }
        public int Quantity { get; set; } = 1;
        /// <summary>EITHER the sale amount OR the discount (amounts before GST).</summary>
        public decimal? SaleAmount { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string DurationUnit { get; set; }
        public int? DurationCount { get; set; }
        public DateOnly? StartDate { get; set; }
        /// <summary>Leave empty for start + paid duration + free months.</summary>
        public DateOnly? RenewalDate { get; set; }
        public string Notes { get; set; }

        // ---- Order form ----
        public decimal? GstPercent { get; set; }
        public int FreeMonths { get; set; }
        public int BillingFrequencyId { get; set; }
        public int BillingMethodId { get; set; }
        public int PaymentMethodId { get; set; }
        public int PaymentTermId { get; set; }
        public int CurrencyId { get; set; }
        public OrderContactParams PrimaryContact { get; set; }
        /// <summary>Leave empty (or SameAsPrimary) to bill the primary contact.</summary>
        public OrderContactParams BillingContact { get; set; }
        public bool BillingSameAsPrimary { get; set; }
        public string BillToAddress { get; set; }
        /// <summary>"Edit wording by section": the final text of each section for this order.</summary>
        public OrderWording Wording { get; set; }
    }

    public class OrderContactParams
    {
        public string Name { get; set; }
        public string Designation { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }
}
