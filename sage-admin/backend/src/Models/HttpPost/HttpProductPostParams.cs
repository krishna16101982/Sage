using System.Collections.Generic;

namespace SageAdminApi.Models.HttpPost
{
    public class HttpProductCategoryPostParams
    {
        /// <summary>Immutable once created.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public int SortOrder { get; set; }
    }

    public class HttpProductCategoryPutParams
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class HttpProductPostParams
    {
        public long CategoryId { get; set; }
        /// <summary>Immutable once created.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public int SortOrder { get; set; }
    }

    public class HttpProductPutParams
    {
        /// <summary>The product may move to another active category.</summary>
        public long CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class HttpProductPlanPostParams
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        /// <summary>Month, Quarter or Year.</summary>
        public string DurationUnit { get; set; } = "Year";
        public int DurationCount { get; set; } = 1;
        /// <summary>Required. Per unit, INR, at most 2 decimals.</summary>
        public decimal? Price { get; set; }
        /// <summary>Required. Per unit, INR, at most 2 decimals.</summary>
        public decimal? Cost { get; set; }
        public int SortOrder { get; set; }
    }

    public class HttpProductPlanPutParams : HttpProductPlanPostParams
    {
        public bool IsActive { get; set; } = true;
    }

    /// <summary>All seat ranges of a plan for one financial year. An empty list removes that year.</summary>
    public class HttpPlanSeatPricesPutParams
    {
        public List<HttpSeatPriceRange> Ranges { get; set; } = new List<HttpSeatPriceRange>();
    }

    public class HttpSeatPriceRange
    {
        public int? MinSeats { get; set; }
        /// <summary>Blank = "and above" (last range only).</summary>
        public int? MaxSeats { get; set; }
        /// <summary>Per seat, INR, at most 2 decimals.</summary>
        public decimal? Price { get; set; }
        /// <summary>Optional; blank = the plan's own cost.</summary>
        public decimal? Cost { get; set; }
    }
}
