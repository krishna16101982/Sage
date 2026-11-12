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
}
