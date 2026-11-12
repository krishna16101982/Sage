using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.Models.Paging
{
    /// <summary>
    /// Validated paging values used by data functions (offset based). Built by Pagination.GetPagingParams.
    /// </summary>
    public class PaginationParams : IPaginationParams
    {
        public const int MaxPageSize = 200;
        public const int DefaultPageSize = 20;

        private int _page = 1;
        private int _pageSize = DefaultPageSize;

        public PaginationParams()
        {
            //Initialize properties to default values - very important
            SortBy = "Id";
            SortDirection = "DESC";
            TotalRows = 0;
        }

        public int Page { get => _page; set => _page = value < 1 ? 1 : value; }
        public int PageSize { get => _pageSize; set => _pageSize = value < 1 ? DefaultPageSize : System.Math.Min(value, MaxPageSize); }
        public int Offset => (Page - 1) * PageSize;
        public string SortBy { get; set; }
        public string SortDirection { get; set; }
        public int TotalRows { get; set; }
    }
}
