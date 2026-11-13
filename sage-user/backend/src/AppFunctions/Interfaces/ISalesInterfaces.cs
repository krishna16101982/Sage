using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SageUserApi.Models;
using SageUserApi.Models.Filter;
using SageUserApi.Models.HttpPost;

namespace SageUserApi.AppFunctions.Interfaces
{
    public interface ISalesUserContextFunctions
    {
        /// <summary>The active sales user with this public id, or null.</summary>
        Task<SalesUserContext> GetAsync(Guid _publicId, CancellationToken _ct = default);
    }

    public interface ICatalogFunctions
    {
        /// <summary>Company rules for the sale page (maximum discount, backdating limit, sale number prefix).</summary>
        Task<SalesRules> GetSalesRulesAsync(CancellationToken _ct = default);
        /// <summary>Active categories > products > plans that can be sold (products without an active plan are left out).</summary>
        Task<(List<CatalogCategory> catalog, IFunctionReturn functionReturn)> GetCatalogAsync(CancellationToken _ct = default);
    }

    public interface ICustomerFunctions
    {
        Task<(PagedResult<CustomerDto> result, IFunctionReturn functionReturn)> GetCustomersAsync(CustomerFilter _filter, CancellationToken _ct = default);
        Task<(CustomerDto customer, IFunctionReturn functionReturn)> GetCustomerAsync(Guid _id, CancellationToken _ct = default);
        Task<(CustomerDto customer, IFunctionReturn functionReturn)> CreateCustomerAsync(HttpCustomerPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
    }

    public interface ISaleFunctions
    {
        Task<(SalesPage result, IFunctionReturn functionReturn)> GetSalesAsync(SaleFilter _filter, SalesUserContext _user, CancellationToken _ct = default);
        /// <summary>A sale the user made, or one made by a member of their team.</summary>
        Task<(SaleDto sale, IFunctionReturn functionReturn)> GetSaleAsync(Guid _id, SalesUserContext _user, CancellationToken _ct = default);
        Task<(SaleDto sale, IFunctionReturn functionReturn)> CreateSaleAsync(HttpSalePostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        Task<(SaleDto sale, IFunctionReturn functionReturn)> CancelSaleAsync(Guid _id, HttpSaleCancelParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        Task<(List<TeamMember> members, IFunctionReturn functionReturn)> GetTeamMembersAsync(SalesUserContext _user, CancellationToken _ct = default);
        /// <summary>A sales person (allowed by their manager) asks to cancel one of their own sales.</summary>
        Task<(CancelRequestDto request, IFunctionReturn functionReturn)> CreateCancelRequestAsync(Guid _saleId, HttpSaleCancelParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        /// <summary>Managers: requests on their team's sales. Sales persons: their own requests.</summary>
        Task<(List<CancelRequestDto> requests, IFunctionReturn functionReturn)> GetCancelRequestsAsync(string _status, SalesUserContext _user, CancellationToken _ct = default);
        Task<(CancelRequestDto request, IFunctionReturn functionReturn)> DecideCancelRequestAsync(Guid _requestId, bool _approve, HttpCancelDecisionParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        Task<(TeamMember member, IFunctionReturn functionReturn)> SetCancelPermissionAsync(Guid _memberId, HttpCancelPermissionParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
    }
}
