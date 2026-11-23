using System;
using SageCommonLib.Models.Paging;

namespace SageUserApi.Models.HttpPost
{
    /// <summary>Body of POST / PUT api/user/leads. The company (CustomerId) is only read when the lead is added.</summary>
    public class HttpLeadPostParams
    {
        public Guid? CustomerId { get; set; }
        public string ClientName { get; set; }
        public string ClientDesignation { get; set; }
        public string ClientEmail { get; set; }
        public string ClientPhone { get; set; }
        public string SecondaryClientName { get; set; }
        public string SecondaryClientDesignation { get; set; }
        public string SecondaryClientEmail { get; set; }
        public string SecondaryClientPhone { get; set; }
        public int? ProductId { get; set; }
        public int? SourceId { get; set; }
        public string Requirement { get; set; }
        public int StatusId { get; set; }
        public int TypeId { get; set; }
        public Guid? GeneratedById { get; set; }
        public Guid? AssignedId { get; set; }
        public decimal? MonthlyExpectedRevenue { get; set; }
        public int? BillingCycleId { get; set; }
        public DateOnly? NextFollowUpDate { get; set; }
        /// <summary>"Created Date" on the form.</summary>
        public DateOnly? LeadDate { get; set; }
        public DateOnly? ClosingDate { get; set; }
        public string Remarks { get; set; }
    }
}

namespace SageUserApi.Models.Filter
{
    /// <summary>Query string of GET api/user/leads (paged in dbo.usp_Lead_GetPaged).</summary>
    public class LeadFilter : HttpPaging
    {
        /// <summary>Lead number, company, client or person name.</summary>
        public string Search { get; set; }
        public int? StatusId { get; set; }
        public int? ProductId { get; set; }
        public Guid? AssignedId { get; set; }
        public Guid? GeneratedById { get; set; }
        /// <summary>lt7, 7to30, 31to60 or gt60 (days since the created date).</summary>
        public string Aging { get; set; }
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        /// <summary>Only leads no order was made from yet (the order form's lead picker).</summary>
        public bool WithoutOrder { get; set; }
    }
}
