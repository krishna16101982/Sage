using System;
using System.Collections.Generic;

namespace SageAdminApi.Models
{
    /// <summary>One value of a lead list (dbo.LeadProducts, dbo.LeadSources or dbo.LeadBillingCycles).</summary>
    public class LeadListValueDto
    {
        public int Id { get; set; }
        /// <summary>Product, Source or BillingCycle.</summary>
        public string List { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Leads that use this value (a value in use is turned off, not removed).</summary>
        public int UsedBy { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    /// <summary>A lead list with its values in order.</summary>
    public class LeadList
    {
        public string List { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<LeadListValueDto> Values { get; set; } = new List<LeadListValueDto>();
    }
}

namespace SageAdminApi.Models.HttpPost
{
    /// <summary>POST / PUT api/admin/lead-lists: one value of a lead list.</summary>
    public class HttpLeadListValuePostParams
    {
        /// <summary>Product, Source or BillingCycle.</summary>
        public string List { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

namespace SageAdminApi.Models
{
    /// <summary>One figure of the lead dashboard (a status, source, aging bucket, lead type or product).</summary>
    public class LeadSummaryItem
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>Leads.</summary>
        public int Total { get; set; }
        /// <summary>Won leads.</summary>
        public int Won { get; set; }
        /// <summary>Monthly expected revenue of the won leads (INR).</summary>
        public decimal Revenue { get; set; }
    }

    /// <summary>GET api/admin/leads/summary: the Admin dashboard (from dbo.usp_Lead_Summary), all leads or one sales user's.</summary>
    public class LeadSummary
    {
        public int TotalLeads { get; set; }
        public int WonLeads { get; set; }
        /// <summary>Won ÷ total leads, in % (2 decimals); 0 when there are no leads.</summary>
        public decimal ConversionRate { get; set; }
        /// <summary>Monthly expected revenue of the won leads, and the same × 12.</summary>
        public decimal WonRevenueMonthly { get; set; }
        public decimal WonRevenueAnnualized { get; set; }
        public List<LeadSummaryItem> Statuses { get; set; } = new List<LeadSummaryItem>();
        public List<LeadSummaryItem> Sources { get; set; } = new List<LeadSummaryItem>();
        public List<LeadSummaryItem> Aging { get; set; } = new List<LeadSummaryItem>();
        public List<LeadSummaryItem> Types { get; set; } = new List<LeadSummaryItem>();
        /// <summary>Won leads and their monthly revenue per product.</summary>
        public List<LeadSummaryItem> Products { get; set; } = new List<LeadSummaryItem>();
    }
}
