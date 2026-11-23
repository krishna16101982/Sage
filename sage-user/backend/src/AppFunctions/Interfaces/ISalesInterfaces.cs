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
        /// <summary>
        /// Active categories > products > plans that can be sold (products without an active plan are left out). Seat prices
        /// are this financial year's, or _fiscalYear's (an order being edited keeps its order date's year).
        /// </summary>
        Task<(List<CatalogCategory> catalog, IFunctionReturn functionReturn)> GetCatalogAsync(int? _fiscalYear = null, CancellationToken _ct = default);
    }

    public interface ICustomerFunctions
    {
        Task<(PagedResult<CustomerDto> result, IFunctionReturn functionReturn)> GetCustomersAsync(CustomerFilter _filter, CancellationToken _ct = default);
        Task<(CustomerDto customer, IFunctionReturn functionReturn)> GetCustomerAsync(Guid _id, CancellationToken _ct = default);
        Task<(CustomerDto customer, IFunctionReturn functionReturn)> CreateCustomerAsync(HttpCustomerPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        /// <summary>Changes a company's contact, GSTIN and address; the name stays as it is.</summary>
        Task<(CustomerDto customer, IFunctionReturn functionReturn)> UpdateCustomerAsync(Guid _id, HttpCustomerPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
    }

    /// <summary>The New order form: lists and defaults, saving an order, its details for a sale.</summary>
    /// <summary>The order form PDF, saved by order number (see OrderFormPdfSettings).</summary>
    public interface IOrderFormPdfFunctions
    {
        /// <summary>Makes and saves the PDF of a just-saved order; failures are logged, not thrown.</summary>
        Task CreateAsync(Guid _saleId, CancellationToken _ct = default);
        /// <summary>The saved PDF (made now if missing) of an order the user may see.</summary>
        Task<(OrderFormPdfFile file, IFunctionReturn functionReturn)> GetAsync(Guid _saleId, SalesUserContext _user, CancellationToken _ct = default);
    }

    /// <summary>Lead Records: everyone sees every lead; the creator and their manager change it; managers delete it.</summary>
    public interface ILeadFunctions
    {
        /// <summary>One page of leads (dbo.usp_Lead_GetPaged), each with what _user may do with it.</summary>
        Task<(PagedResult<LeadDto> result, IFunctionReturn functionReturn)> GetLeadsAsync(LeadFilter _filter, SalesUserContext _user, CancellationToken _ct = default);
        Task<(LeadDto lead, IFunctionReturn functionReturn)> GetLeadAsync(Guid _id, SalesUserContext _user, CancellationToken _ct = default);
        /// <summary>The Home dashboard figures (dbo.usp_Lead_Summary): every lead, or only _user's (_mine).</summary>
        Task<(LeadSummary summary, IFunctionReturn functionReturn)> GetSummaryAsync(bool _mine, SalesUserContext _user, CancellationToken _ct = default);
        /// <summary>Product, source and billing-cycle lists (active values), statuses, lead types and sales users.</summary>
        Task<(LeadOptions options, IFunctionReturn functionReturn)> GetOptionsAsync(CancellationToken _ct = default);
        /// <summary>Adds (_id null) or changes a lead; the company cannot change.</summary>
        Task<(LeadDto lead, IFunctionReturn functionReturn)> SaveLeadAsync(Guid? _id, HttpLeadPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        Task<IFunctionReturn> DeleteLeadAsync(Guid _id, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        /// <summary>An order saved with "No lead": creates an Open lead for its company and primary contact and links the order.
        /// Returns the lead, or null (failures are logged; the order stays saved).</summary>
        Task<LeadDto> CreateForOrderAsync(Guid _saleId, Guid _companyId, OrderContactParams _contact, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        /// <summary>Links an order saved from a Won lead (New Order) to the lead; failures are logged only.</summary>
        Task LinkOrderAsync(Guid _leadId, Guid _saleId, SalesUserContext _user, CancellationToken _ct = default);
    }

    public interface IOrderFormFunctions
    {
        Task<(OrderFormOptions options, IFunctionReturn functionReturn)> GetOptionsAsync(CancellationToken _ct = default);
        Task<(SaleDto sale, IFunctionReturn functionReturn)> CreateOrderAsync(HttpOrderFormPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        /// <summary>Edits an active order-form order (the seller or their manager); same checks and price rules as saving it.</summary>
        /// <summary>Order forms list: Draft / Sent to DocuSign / Signed, or Cancelled (cancels the sale, or asks the manager to).</summary>
        Task<(SaleDto sale, IFunctionReturn functionReturn)> SetTrackingAsync(Guid _saleId, HttpOrderTrackingParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        Task<(SaleDto sale, IFunctionReturn functionReturn)> UpdateOrderAsync(Guid _saleId, HttpOrderFormPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        Task<(OrderDetailsDto details, IFunctionReturn functionReturn)> GetOrderDetailsAsync(Guid _saleId, SalesUserContext _user, CancellationToken _ct = default);
    }

    public interface ISaleFunctions
    {
        Task<(SalesPage result, IFunctionReturn functionReturn)> GetSalesAsync(SaleFilter _filter, SalesUserContext _user, CancellationToken _ct = default);
        /// <summary>A sale the user made, or one made by a member of their team.</summary>
        Task<(SaleDto sale, IFunctionReturn functionReturn)> GetSaleAsync(Guid _id, SalesUserContext _user, CancellationToken _ct = default);
        Task<(SaleDto sale, IFunctionReturn functionReturn)> CreateSaleAsync(HttpSalePostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        Task<(SaleDto sale, IFunctionReturn functionReturn)> CancelSaleAsync(Guid _id, HttpSaleCancelParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        Task<(List<TeamMember> members, IFunctionReturn functionReturn)> GetTeamMembersAsync(SalesUserContext _user, CancellationToken _ct = default);
        /// <summary>A sales manager adds a sales person to their own team (temporary password, changed at first sign-in).</summary>
        /// <summary>A sales manager activates or deactivates a member of their own team (deactivating signs them out).</summary>
        Task<(TeamMember member, IFunctionReturn functionReturn)> SetTeamMemberStatusAsync(Guid _memberId, HttpTeamMemberStatusParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        Task<(TeamMember member, IFunctionReturn functionReturn)> CreateTeamMemberAsync(HttpTeamMemberPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        /// <summary>A sales person (allowed by their manager) asks to cancel one of their own sales.</summary>
        Task<(CancelRequestDto request, IFunctionReturn functionReturn)> CreateCancelRequestAsync(Guid _saleId, HttpSaleCancelParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        /// <summary>Managers: requests on their team's sales. Sales persons: their own requests.</summary>
        /// <summary>One page of cancellation requests (dbo.usp_SaleCancelRequest_GetPaged): a manager's team's, or a sales person's own.</summary>
        Task<(PagedResult<CancelRequestDto> result, IFunctionReturn functionReturn)> GetCancelRequestsAsync(string _status, HttpPaging _paging, SalesUserContext _user, CancellationToken _ct = default);
        Task<(CancelRequestDto request, IFunctionReturn functionReturn)> DecideCancelRequestAsync(Guid _requestId, bool _approve, HttpCancelDecisionParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
        Task<(TeamMember member, IFunctionReturn functionReturn)> SetCancelPermissionAsync(Guid _memberId, HttpCancelPermissionParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default);
    }
}
