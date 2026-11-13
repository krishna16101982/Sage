using System;
using System.Collections.Generic;
using SageCommonLib.Models.Paging;

namespace SageUserApi.Models
{
    /// <summary>The signed-in sales user as the business functions need it (internal id and role).</summary>
    public class SalesUserContext
    {
        public long Id { get; set; }
        public Guid PublicId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool CanRequestCancellation { get; set; }
        public bool IsManager => Role == Globals.ApplicationDatabaseConstants.SalesRoles.SalesManager;
    }

    #region Catalog (what can be sold)

    public class CatalogPlan
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public string DurationUnit { get; set; } = string.Empty;
        public int DurationCount { get; set; }
        /// <summary>List price per unit (INR) for one plan term.</summary>
        public decimal Price { get; set; }
        /// <summary>Cost per unit (INR) for one plan term.</summary>
        public decimal Cost { get; set; }
    }

    public class CatalogProduct
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public List<CatalogPlan> Plans { get; set; } = new List<CatalogPlan>();
    }

    public class CatalogCategory
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<CatalogProduct> Products { get; set; } = new List<CatalogProduct>();
    }

    #endregion

    public class CustomerDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Gstin { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByName { get; set; }
        /// <summary>Active sales to this customer (all sellers).</summary>
        public int SaleCount { get; set; }
    }

    public class SaleDto
    {
        public Guid Id { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public Guid SellerId { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        public decimal ListAmount { get; set; }
        public decimal SaleAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        /// <summary>Discount as a percentage of the list amount (2 decimals).</summary>
        public decimal DiscountPercent { get; set; }
        public decimal CostAmount { get; set; }
        /// <summary>SaleAmount - CostAmount.</summary>
        public decimal Margin { get; set; }
        public string DurationUnit { get; set; } = string.Empty;
        public int DurationCount { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly RenewalDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public string CancelledByName { get; set; }
        public string CancelReason { get; set; }
        /// <summary>A cancellation request is waiting for the manager.</summary>
        public bool HasPendingCancelRequest { get; set; }
        /// <summary>Cancellation requests for this sale, newest first (single-sale view only).</summary>
        public List<CancelRequestDto> CancelRequests { get; set; }
    }

    /// <summary>A sales person's request to cancel one of their sales, approved or rejected by their manager.</summary>
    public class CancelRequestDto
    {
        public Guid Id { get; set; }
        public Guid SaleId { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal SaleAmount { get; set; }
        public Guid RequestedById { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        /// <summary>Pending, Approved or Rejected.</summary>
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedAtUtc { get; set; }
        public string DecidedByName { get; set; }
        public DateTime? DecidedAtUtc { get; set; }
        public string DecisionNote { get; set; }
    }

    /// <summary>A page of sales plus totals of the matching active sales.</summary>
    public class SalesPage : PagedResult<SaleDto>
    {
        public decimal TotalSaleAmount { get; set; }
        public decimal TotalMargin { get; set; }
    }

    /// <summary>Company rules for recording a sale (Admin Panel > Settings).</summary>
    public class SalesRules
    {
        /// <summary>Largest discount allowed, in % of the list amount (100 = no limit).</summary>
        public decimal MaxDiscountPercent { get; set; } = 100;
        /// <summary>How many days in the past a start date may be.</summary>
        public int MaxBackdateDays { get; set; } = 365;
        public string SaleNumberPrefix { get; set; } = "S";
    }

    /// <summary>A sales person in the signed-in manager's team (for the Team sales filter).</summary>
    public class TeamMember
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        /// <summary>May ask for their sales to be cancelled (the manager approves or rejects).</summary>
        public bool CanRequestCancellation { get; set; }
        /// <summary>Requests from this person waiting for the manager.</summary>
        public int PendingRequests { get; set; }
    }
}
