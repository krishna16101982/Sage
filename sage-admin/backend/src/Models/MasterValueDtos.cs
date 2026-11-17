using System;
using System.Collections.Generic;

namespace SageAdminApi.Models
{
    /// <summary>One value of an order-form list (dbo.MasterValues).</summary>
    public class MasterValueDto
    {
        public int Id { get; set; }
        /// <summary>BillingFrequency, BillingMethod, PaymentMethod, PaymentTerm or Currency.</summary>
        public string MasterType { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Orders that use this value (a value in use can be turned off, not removed).</summary>
        public int UsedBy { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    /// <summary>The order-form lists, each with its values in order.</summary>
    public class MasterList
    {
        public string MasterType { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<MasterValueDto> Values { get; set; } = new List<MasterValueDto>();
    }

    /// <summary>Order-form details saved on a sale (null for a sale from the simple page).</summary>
    public class AdminOrderDetails
    {
        public int FreeMonths { get; set; }
        public string BillingFrequency { get; set; }
        public string BillingMethod { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentTerm { get; set; }
        public string Currency { get; set; }
        public string PrimaryContactName { get; set; }
        public string PrimaryContactDesignation { get; set; }
        public string PrimaryContactEmail { get; set; }
        public string PrimaryContactPhone { get; set; }
        public string BillingContactName { get; set; }
        public string BillingContactDesignation { get; set; }
        public string BillingContactEmail { get; set; }
        public string BillingContactPhone { get; set; }
        public string BillToAddress { get; set; }
        /// <summary>The order form's wording sections as saved (JSON object), or null.</summary>
        public System.Text.Json.JsonElement? Wording { get; set; }
        /// <summary>Seat-priced plans: the financial year (2027 = FY27) and seat range ("10-19", "50000+") the price came from.</summary>
        public int? PriceFiscalYear { get; set; }
        public string PriceSeatRange { get; set; }
    }
}

namespace SageAdminApi.Models.HttpPost
{
    /// <summary>POST / PUT api/admin/masters: one list value.</summary>
    public class HttpMasterValuePostParams
    {
        public string MasterType { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
