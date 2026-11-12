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
    }
}
