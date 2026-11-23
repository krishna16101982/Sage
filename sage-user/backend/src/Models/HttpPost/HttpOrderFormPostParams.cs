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
        /// <summary>Quantity of the plan's included item on this order (e.g. 1000 Team Link Extend seats); empty = none.</summary>
        public int? IncludedItemQuantity { get; set; }
        /// <summary>New order from a Won lead (Lead Records > New Order): the saved order is linked to the lead.</summary>
        public Guid? LeadId { get; set; }
        /// <summary>Tracking status (DDL 036): Draft 1 (default), Sent to DocuSign 2, Signed 3. Cancelled 4 (edit only) cancels
        /// the sale (manager) or asks the manager to (sales person), with CancelReason.</summary>
        public int? TrackingStatusId { get; set; }
        public string CancelReason { get; set; }
    }

    /// <summary>Body of PUT api/user/order-form/{saleId}/tracking (the Order forms list).</summary>
    public class HttpOrderTrackingParams
    {
        public int TrackingStatusId { get; set; }
        /// <summary>Needed for Cancelled.</summary>
        public string Reason { get; set; }
    }

    public class OrderContactParams
    {
        public string Name { get; set; }
        public string Designation { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }
}
