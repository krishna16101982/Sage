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
