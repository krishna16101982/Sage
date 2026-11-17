using System.Collections.Generic;

namespace SageAdminApi.Models
{
    public class ProductCategoryDto
    {
        public long Id { get; set; }
        /// <summary>Immutable once created.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public List<ProductDto> Products { get; set; } = new List<ProductDto>();
    }

    public class ProductDto
    {
        public long Id { get; set; }
        public long CategoryId { get; set; }
        /// <summary>Immutable once created.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public List<ProductPlanDto> Plans { get; set; } = new List<ProductPlanDto>();
    }

    /// <summary>A price-list entry. Price and Cost are per unit (INR) for one term of DurationCount x DurationUnit.</summary>
    public class ProductPlanDto
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        /// <summary>Month, Quarter or Year.</summary>
        public string DurationUnit { get; set; } = string.Empty;
        public int DurationCount { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        /// <summary>True when the plan is priced by seat range (dbo.ProductPlanSeatPrices); Price/Cost are then only a fallback.</summary>
        public bool HasSeatPrices { get; set; }
    }

    /// <summary>One seat range of a plan for one financial year. Price and Cost are per seat for one plan term.</summary>
    public class SeatPriceRange
    {
        public int MinSeats { get; set; }
        /// <summary>Null = "and above".</summary>
        public int? MaxSeats { get; set; }
        public decimal Price { get; set; }
        /// <summary>Null = the plan's own cost.</summary>
        public decimal? Cost { get; set; }
    }

    /// <summary>A plan's seat ranges for one Indian financial year (2027 = FY27 = Apr 2026 to Mar 2027).</summary>
    public class PlanSeatPriceYear
    {
        public int FiscalYear { get; set; }
        public List<SeatPriceRange> Ranges { get; set; } = new List<SeatPriceRange>();
    }
}
