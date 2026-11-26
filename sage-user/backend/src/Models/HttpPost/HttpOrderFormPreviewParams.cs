using System;

namespace SageUserApi.Models.HttpPost
{
    /// <summary>
    /// Body of POST api/user/order-form/preview: the order form as it is filled in on the page, not saved. The page sends
    /// what it shows (names, dates and the amounts of its order summary); the API cleans it and prints it as a PDF or a
    /// Word file. Nothing is written to the database. The saved order's own PDF is made from the database when it is saved.
    /// </summary>
    public class HttpOrderFormPreviewParams
    {
        public string CustomerName { get; set; }
        /// <summary>The product's code, for its logo (e.g. LinkedIn).</summary>
        public string ProductCode { get; set; }
        public DateOnly? OrderDate { get; set; }
        public DateOnly? StartDate { get; set; }
        /// <summary>Last day of the term.</summary>
        public DateOnly? EndDate { get; set; }
        public int TermMonths { get; set; }
        public int FreeMonths { get; set; }
        public OrderContactParams PrimaryContact { get; set; }
        public string BillingFrequency { get; set; }
        public string BillingMethod { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentTerm { get; set; }
        public string Currency { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal ListAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal SaleAmount { get; set; }
        public decimal GstPercent { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public int? IncludedItemQuantity { get; set; }
        public OrderWording Wording { get; set; }
    }
}
