using System;
using System.Collections.Generic;

namespace SageUserApi.Models
{
    /// <summary>One value of an order-form list (dbo.MasterValues).</summary>
    public class MasterOption
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>Filled in on a new order form.</summary>
        public bool IsDefault { get; set; }
    }

    /// <summary>GET api/user/order-form/options: the lists and defaults for a new order form.</summary>
    public class OrderFormOptions
    {
        public decimal DefaultGstPercent { get; set; } = 18;
        public int MaxFreeMonths { get; set; } = 12;
        public decimal MaxDiscountPercent { get; set; } = 100;
        public int MaxBackdateDays { get; set; } = 365;
        public List<MasterOption> BillingFrequencies { get; set; } = new List<MasterOption>();
        public List<MasterOption> BillingMethods { get; set; } = new List<MasterOption>();
        public List<MasterOption> PaymentMethods { get; set; } = new List<MasterOption>();
        public List<MasterOption> PaymentTerms { get; set; } = new List<MasterOption>();
        public List<MasterOption> Currencies { get; set; } = new List<MasterOption>();
        /// <summary>Standard wording (placeholders {freeMonths} and {gstPercent} are filled in by the form).</summary>
        public OrderWording Wording { get; set; } = new OrderWording();
    }

    /// <summary>
    /// The order form's wording, section by section ("Edit wording by section"). The standard text comes from Settings >
    /// Order form wording; the seller may change any section for one order. Lines are separated by new lines.
    /// </summary>
    public class OrderWording
    {
        public string SignReturnLine { get; set; }
        public string SignReturnInstruction { get; set; }
        public string OrderNotes { get; set; }
        /// <summary>Only used when the order has free months.</summary>
        public string FreeMonthNote { get; set; }
        /// <summary>An extra line item printed with the product (e.g. bundled seats); optional.</summary>
        public string ExtraItemTitle { get; set; }
        public string ExtraItemDescription { get; set; }
        public string ProductTitle { get; set; }
        public string ProductDescription { get; set; }
        public string PeriodLine { get; set; }
        public string TaxInformation { get; set; }
        /// <summary>One clause per line.</summary>
        public string SignatureClauses { get; set; }
        /// <summary>One line per row.</summary>
        public string Footer { get; set; }
    }

    /// <summary>A contact on an order form.</summary>
    public class OrderContact
    {
        public string Name { get; set; }
        public string Designation { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    /// <summary>GET api/user/order-form/{saleId}: the order-form details saved on a sale (null fields for a simple sale).</summary>
    public class OrderDetailsDto
    {
        public Guid SaleId { get; set; }
        /// <summary>True when the sale was recorded with the order form.</summary>
        public bool HasOrderForm { get; set; }
        public decimal GstPercent { get; set; }
        public decimal TaxAmount { get; set; }
        /// <summary>SaleAmount + TaxAmount.</summary>
        public decimal TotalAmount { get; set; }
        public int FreeMonths { get; set; }
        public string BillingFrequency { get; set; }
        public string BillingMethod { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentTerm { get; set; }
        public string Currency { get; set; }
        public OrderContact PrimaryContact { get; set; }
        public OrderContact BillingContact { get; set; }
        public string BillToAddress { get; set; }
        /// <summary>The wording this order was saved with (null for a sale from the simple page).</summary>
        public OrderWording Wording { get; set; }
        /// <summary>Seat-priced plans: the financial year (2027 = FY27) and seat range the price came from, e.g. "10-19" or "50000+". Null = the plan's single price.</summary>
        public int? PriceFiscalYear { get; set; }
        public string PriceSeatRange { get; set; }
        /// <summary>Quantity of the included item (e.g. 1000 Team Link Extend seats), or null.</summary>
        public int? IncludedItemQuantity { get; set; }
    }
}
