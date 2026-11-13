using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SageAdminApi.Models;
using SageAdminApi.AppFunctions;
using SageAdminApi.Models.Filter;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.AppFunctions.Interfaces
{
    public interface ICustomerFunctions
    {
        Task<(PagedResult<CustomerDto> result, IFunctionReturn functionReturn)> GetCustomersAsync(CustomerFilter _filter, CancellationToken _ct = default);
        Task<(CustomerDto customer, IFunctionReturn functionReturn)> GetCustomerAsync(Guid _id, CancellationToken _ct = default);
        Task<(CustomerDto customer, IFunctionReturn functionReturn)> CreateCustomerAsync(HttpCustomerPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(CustomerDto customer, IFunctionReturn functionReturn)> UpdateCustomerAsync(Guid _id, HttpCustomerPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
    }

    /// <summary>Read-only sales reporting for the Admin Panel (sales are recorded in the User Panel).</summary>
    public interface ISalesReportFunctions
    {
        Task<(AdminSalesPage result, IFunctionReturn functionReturn)> GetSalesAsync(AdminSaleFilter _filter, CancellationToken _ct = default);
        Task<(AdminSaleDto sale, IFunctionReturn functionReturn)> GetSaleAsync(Guid _id, CancellationToken _ct = default);
        Task<(List<SalesSummaryRow> rows, IFunctionReturn functionReturn)> GetSummaryAsync(SalesSummaryFilter _filter, CancellationToken _ct = default);
        Task<(List<SellerOption> sellers, IFunctionReturn functionReturn)> GetSellersAsync(CancellationToken _ct = default);
        Task<(ExportFile file, IFunctionReturn functionReturn)> ExportSalesAsync(AdminSaleFilter _filter, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
        Task<(ExportFile file, IFunctionReturn functionReturn)> ExportSummaryAsync(SalesSummaryFilter _filter, long? _actorUserId, string _ipAddress, CancellationToken _ct = default);
    }
}
