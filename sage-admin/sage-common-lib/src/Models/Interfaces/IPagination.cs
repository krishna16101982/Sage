using System.Collections.Generic;
using SageCommonLib.Models.Paging;

namespace SageCommonLib.Models.Interfaces
{
    public interface IPagination
    {
        PaginationParams GetPagingParams(HttpPaging _httpPaging, IEnumerable<string> _allowedSortColumns, string _defaultSortBy);
    }

    public interface IPaginationParams
    {
        int Page { get; set; }
        int PageSize { get; set; }
        int Offset { get; }
        string SortBy { get; set; }
        string SortDirection { get; set; }
        int TotalRows { get; set; }
    }

    public interface IHttpPaging
    {
        int Page { get; set; }
        int PageSize { get; set; }
        string SortBy { get; set; }
        string SortDirection { get; set; }
    }
}
