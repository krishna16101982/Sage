using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.Filter;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Customers from the Admin Panel: list, add and edit. The table is shared with the User Panel (sales users add
    /// customers there too); the same duplicate rules apply (same GSTIN, or same name in the same city).
    /// </summary>
    public partial class CustomerFunctions : ICustomerFunctions
    {
        private static readonly string[] SortKeys = { "name", "city", "createdAtUtc" };

        /// <summary>Same columns, in the same order, as dbo.usp_Customer_GetPaged.</summary>
        private const string CustomerSelect = @"SELECT c.PublicId, c.Name, c.ContactPerson, c.Email, c.Phone, c.GSTIN, c.Address, c.City, c.State, c.Country, c.CreatedAtUtc,
    CASE WHEN u.Id IS NOT NULL THEN CONCAT(u.FirstName, N' ', u.LastName) WHEN a.Id IS NOT NULL THEN CONCAT(a.FirstName, N' ', a.LastName, N' (Admin)') END,
    (SELECT COUNT(*) FROM dbo.Sales s WHERE s.CustomerId = c.Id AND s.Status = 'Active')
FROM dbo.Customers c LEFT JOIN dbo.SalesUsers u ON u.Id = c.CreatedBySalesUserId LEFT JOIN dbo.AdminUsers a ON a.Id = c.CreatedByAdminUserId
WHERE c.PublicId = @id AND c.IsActive = 1";

        internal static class Messages
        {
            public const string NameRequired = "Customer name is required (up to 200 characters).";
            public const string ContactPersonTooLong = "Contact person must be 150 characters or fewer.";
            public const string InvalidEmail = "Enter a valid email address.";
            public const string InvalidPhone = "Enter a valid phone number.";
            public const string InvalidGstin = "Enter a valid 15-character GSTIN (e.g. 27AAPFU0939F1ZV).";
            public const string AddressTooLong = "Address must be 500 characters or fewer.";
            public const string PlaceTooLong = "City, state and country must be 100 characters or fewer.";
            public const string NotFound = "Customer not found.";
        }

        [GeneratedRegex(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]Z[0-9A-Z]$")]
        private static partial Regex GstinRegex();

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly IAuditFunctions _auditFunctions;

        public CustomerFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IAuditFunctions __auditFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _auditFunctions = __auditFunctions;
        }

        #region Helpers

        private static CustomerDto MapCustomer(SqlDataReader r) => new CustomerDto
        {
            Id = r.GetGuid(0),
            Name = r.GetString(1),
            ContactPerson = S(r, 2),
            Email = S(r, 3),
            Phone = S(r, 4),
            Gstin = S(r, 5),
            Address = S(r, 6),
            City = S(r, 7),
            State = S(r, 8),
            Country = r.GetString(9),
            CreatedAtUtc = DateTime.SpecifyKind(r.GetDateTime(10), DateTimeKind.Utc),
            CreatedByName = S(r, 11),
            SaleCount = r.GetInt32(12)
        };

        private Task<CustomerDto> GetRowAsync(Guid _id, CancellationToken _ct)
            => _databaseFunctions.QuerySingleOrDefaultAsync(CustomerSelect, MapCustomer, new[] { P("@id", _id) }, _ct);

        private static object Snapshot(CustomerDto c) => c == null ? null : new { c.Name, c.ContactPerson, c.Email, c.Phone, c.Gstin, c.Address, c.City, c.State, c.Country };

        /// <summary>Validates and normalises the input; returns the procedure parameters (without the actor) or the errors.</summary>
        private static (List<SqlParameter> parameters, List<string> errors) Prepare(HttpCustomerPostParams p)
        {
            p ??= new HttpCustomerPostParams();
            string _name = Clean(p.Name);
            string _email = p.Email.NullIfWhiteSpace();
            string _phone = p.Phone.NullIfWhiteSpace();
            string _gstin = p.Gstin.NullIfWhiteSpace()?.ToUpperInvariant();

            var _errors = new List<string>();
            if (string.IsNullOrEmpty(_name) || _name.Length > 200) _errors.Add(Messages.NameRequired);
            if ((p.ContactPerson?.Trim().Length ?? 0) > 150) _errors.Add(Messages.ContactPersonTooLong);
            if (_email != null && !ValidationFunctions.IsValidEmail(_email)) _errors.Add(Messages.InvalidEmail);
            if (_phone != null && !ValidationFunctions.IsValidPhone(_phone)) _errors.Add(Messages.InvalidPhone);
            if (_gstin != null && !GstinRegex().IsMatch(_gstin)) _errors.Add(Messages.InvalidGstin);
            if ((p.Address?.Trim().Length ?? 0) > 500) _errors.Add(Messages.AddressTooLong);
            if ((p.City?.Trim().Length ?? 0) > 100 || (p.State?.Trim().Length ?? 0) > 100 || (p.Country?.Trim().Length ?? 0) > 100) _errors.Add(Messages.PlaceTooLong);

            var _parameters = new List<SqlParameter>
            {
                P("@Name", _name), P("@ContactPerson", Clean(p.ContactPerson)), P("@Email", _email), P("@Phone", _phone), P("@GSTIN", _gstin),
                P("@Address", Clean(p.Address)), P("@City", Clean(p.City)), P("@State", Clean(p.State)), P("@Country", Clean(p.Country) ?? "India")
            };
            return (_parameters, _errors);
        }

        private static IFunctionReturn Invalid(List<string> _errors, string _methodName)
            => new FunctionReturn { Status = false, MessageType = Constants.ReturnMessageTypes.ValidationError, Message = _errors, HttpStatusCode = Constants.HttpStatusCodes.BadRequest, MethodName = _methodName };

        #endregion

        public async Task<(PagedResult<CustomerDto> result, IFunctionReturn functionReturn)> GetCustomersAsync(CustomerFilter _filter, CancellationToken _ct = default)
        {
            string _methodName = "F:Customer:GetCustomers";
            try
            {
                _filter ??= new CustomerFilter();
                PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortKeys, "name");
                if (string.IsNullOrWhiteSpace(_filter.SortDirection)) _paging.SortDirection = "ASC";
                string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
                var _totalCount = IntOut("@TotalCount");

                IReadOnlyList<CustomerDto> _items = await _databaseFunctions.QueryProcedureAsync(SP.CustomerGetPaged, MapCustomer, new List<SqlParameter>
                {
                    P("@Search", _search), P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection),
                    P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                }, _ct);
                int _total = OutInt(_totalCount);
                return (new PagedResult<CustomerDto> { Items = _items, Page = _paging.Page, PageSize = _paging.PageSize, TotalCount = _total }, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(CustomerDto customer, IFunctionReturn functionReturn)> GetCustomerAsync(Guid _id, CancellationToken _ct = default)
        {
            string _methodName = "F:Customer:GetCustomer";
            try
            {
                CustomerDto _customer = await GetRowAsync(_id, _ct);
                return _customer == null ? (null, ApplicationCommonFunctions.NotFound(Messages.NotFound, _methodName)) : (_customer, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(CustomerDto customer, IFunctionReturn functionReturn)> CreateCustomerAsync(HttpCustomerPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Customer:CreateCustomer";
            try
            {
                (List<SqlParameter> _parameters, List<string> _errors) = Prepare(_params);
                if (_errors.Count > 0) return (null, Invalid(_errors, _methodName));
                _parameters.Add(P("@AdminUserId", _actorUserId));

                Guid _id;
                try
                {
                    _id = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.CustomerCreate, _parameters, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                CustomerDto _created = await GetRowAsync(_id, _ct);
                await _auditFunctions.LogAsync(_actorUserId, ApplicationEnumeration.AuditAction.Create.ToString(), ApplicationEnumeration.ObjectType.Customer.ToString(),
                    _id.ToString(), null, Snapshot(_created), _ipAddress, _ct);
                return (_created, new FunctionReturn { MethodName = _methodName, HttpStatusCode = Constants.HttpStatusCodes.Created, Message = new List<string> { "Customer added." } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(CustomerDto customer, IFunctionReturn functionReturn)> UpdateCustomerAsync(Guid _id, HttpCustomerPostParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Customer:UpdateCustomer";
            try
            {
                (List<SqlParameter> _parameters, List<string> _errors) = Prepare(_params);
                if (_errors.Count > 0) return (null, Invalid(_errors, _methodName));

                CustomerDto _existing = await GetRowAsync(_id, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound(Messages.NotFound, _methodName));

                _parameters.Add(P("@PublicId", _id));
                _parameters.Add(P("@AdminUserId", _actorUserId));
                (IFunctionReturn _functionReturn, long _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.CustomerUpdate, _parameters, _methodName, "Customer saved.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                CustomerDto _updated = await GetRowAsync(_id, _ct);
                await _auditFunctions.LogAsync(_actorUserId, ApplicationEnumeration.AuditAction.Update.ToString(), ApplicationEnumeration.ObjectType.Customer.ToString(),
                    _id.ToString(), Snapshot(_existing), Snapshot(_updated), _ipAddress, _ct);
                return (_updated, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
