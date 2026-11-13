using System;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.Models
{
    /// <summary>A customer (shared by the User Panel and the Admin Panel).</summary>
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
        /// <summary>The sales person, or the admin user followed by "(Admin)".</summary>
        public string CreatedByName { get; set; }
        /// <summary>Active sales to this customer.</summary>
        public int SaleCount { get; set; }
    }

    /// <summary>One sale as the Admin Panel sees it (all sellers).</summary>
    public class AdminSaleDto
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
        /// <summary>SalesPerson or SalesManager.</summary>
        public string SellerRole { get; set; } = string.Empty;
        /// <summary>The seller's manager when the sale was made (null for a manager's own sale).</summary>
        public string ManagerName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        public decimal ListAmount { get; set; }
        public decimal SaleAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal CostAmount { get; set; }
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
    }

    /// <summary>A page of sales plus totals of every matching ACTIVE sale.</summary>
    public class AdminSalesPage : PagedResult<AdminSaleDto>
    {
        public decimal TotalSaleAmount { get; set; }
        public decimal TotalMargin { get; set; }
    }

    /// <summary>Totals for one sales person, or for one manager and their team.</summary>
    public class SalesSummaryRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string ManagerName { get; set; }
        public int TeamSize { get; set; }
        public int SaleCount { get; set; }
        public int CancelledCount { get; set; }
        public int Quantity { get; set; }
        public decimal ListAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal SaleAmount { get; set; }
        public decimal CostAmount { get; set; }
        public decimal Margin { get; set; }
    }

    /// <summary>A sales user for the sales filters (everyone who can sell, including inactive ones).</summary>
    public class SellerOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ManagerName { get; set; }
    }
}
