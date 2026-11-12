using System;
using System.Collections.Generic;
using System.Linq;
using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.Models.Paging
{
    public class Pagination : IPagination
    {
        /// <summary>
        /// Convert client paging input into safe paging params. SortBy must be one of the caller's allowed columns
        /// (never concatenate a client-supplied column name into SQL); otherwise the default is used.
        /// </summary>
        public PaginationParams GetPagingParams(HttpPaging _httpPaging, IEnumerable<string> _allowedSortColumns, string _defaultSortBy)
        {
            PaginationParams _paginationParams = new PaginationParams
            {
                Page = _httpPaging.Page,
                PageSize = _httpPaging.PageSize,
                SortBy = _defaultSortBy,
                SortDirection = "DESC"
            };

            string _requested = (_httpPaging.SortBy ?? string.Empty).Trim();
            string? _match = _allowedSortColumns.FirstOrDefault(c => string.Equals(c, _requested, StringComparison.OrdinalIgnoreCase));
            if (_match != null)
            {
                _paginationParams.SortBy = _match;
            }
            if (string.Equals((_httpPaging.SortDirection ?? string.Empty).Trim(), "asc", StringComparison.OrdinalIgnoreCase))
            {
                _paginationParams.SortDirection = "ASC";
            }
            return _paginationParams;
        }
    }

    /// <summary>
    /// One page of results plus totals.
    /// </summary>
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
