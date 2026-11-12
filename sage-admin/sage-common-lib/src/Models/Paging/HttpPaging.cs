using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.Models.Paging
{
    /// <summary>
    /// Paging values as received from the client (query string or body). Never trusted directly: see Pagination.GetPagingParams.
    /// </summary>
    public class HttpPaging : IHttpPaging
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public string SortBy { get; set; } = string.Empty;
        public string SortDirection { get; set; } = string.Empty;
    }
}
