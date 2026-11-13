using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.AppValidations;
using SageUserApi.Globals;
using SageUserApi.Models;
using SageUserApi.Models.Filter;
using SageUserApi.Models.HttpPost;
using Msg = SageUserApi.Globals.ApplicationConstants.Messages;
using SP = SageUserApi.Globals.ApplicationDatabaseConstants.StoredProcedures;

namespace SageUserApi.AppFunctions
{
    /// <summary>Customers are shared by all sales users: anyone can search them and add new ones.</summary>
    public partial class CustomerFunctions : ICustomerFunctions
    {
        private static readonly string[] SortKeys = { "name", "city", "createdAtUtc" };

        /// <summary>Same columns, in the same order, as dbo.usp_Customer_GetPaged.</summary>
        private const string CustomerSelect = @"SELECT c.PublicId, c.Name, c.ContactPerson, c.Email, c.Phone, c.GSTIN, c.Address, c.City, c.State, c.Country, c.CreatedAtUtc,
    CASE WHEN u.Id IS NOT NULL THEN CONCAT(u.FirstName, N' ', u.LastName) WHEN a.Id IS NOT NULL THEN CONCAT(a.FirstName, N' ', a.LastName, N' (Admin)') END,
    (SELECT COUNT(*) FROM dbo.Sales s WHERE s.CustomerId = c.Id AND s.Status = 'Active')
FROM dbo.Customers c LEFT JOIN dbo.SalesUsers u ON u.Id = c.CreatedBySalesUserId LEFT JOIN dbo.AdminUsers a ON a.Id = c.CreatedByAdminUserId
WHERE c.PublicId = @id AND c.IsActive = 1";

        [GeneratedRegex(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]Z[0-9A-Z]$")]
        private static partial Regex GstinRegex();

        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        public CustomerFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
        }

        #region Helpers

        private static SqlParameter P(string _name, object _value) => new SqlParameter(_name, _value ?? DBNull.Value);

        private static string Clean(string _value) => string.IsNullOrWhiteSpace(_value) ? null : Sanitization.Sanitize(_value.Trim());

        private static string S(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

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

        private static List<string> Validate(HttpCustomerPostParams p, string _name, string _email, string _phone, string _gstin)
        {
            var _errors = new List<string>();
            if (string.IsNullOrEmpty(_name)) _errors.Add(Msg.CustomerNameRequired);
            else if (_name.Length > 200) _errors.Add(Msg.CustomerNameTooLong);
            if ((p.ContactPerson?.Trim().Length ?? 0) > 150) _errors.Add(Msg.ContactPersonTooLong);
            if (_email != null && !ValidationFunctions.IsValidEmail(_email)) _errors.Add(Msg.InvalidEmail);
            if (_phone != null && !ValidationFunctions.IsValidPhone(_phone)) _errors.Add(Msg.InvalidPhone);
            if (_gstin != null && !GstinRegex().IsMatch(_gstin)) _errors.Add(Msg.InvalidGstin);
            if ((p.Address?.Trim().Length ?? 0) > 500) _errors.Add(Msg.AddressTooLong);
            if ((p.City?.Trim().Length ?? 0) > 100 || (p.State?.Trim().Length ?? 0) > 100 || (p.Country?.Trim().Length ?? 0) > 100) _errors.Add(Msg.PlaceTooLong);
            return _errors;
        }

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
                var _totalCount = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };

                IReadOnlyList<CustomerDto> _items = await _databaseFunctions.QueryProcedureAsync(SP.CustomerGetPaged, MapCustomer, new List<SqlParameter>
                {
                    P("@Search", _search), P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection),
                    P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                }, _ct);

                int _total = _totalCount.Value is int _count ? _count : 0;
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
                return _customer == null
                    ? (null, ApplicationCommonFunctions.NotFound(Msg.CustomerNotFound, _methodName))
                    : (_customer, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(CustomerDto customer, IFunctionReturn functionReturn)> CreateCustomerAsync(HttpCustomerPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Customer:CreateCustomer";
            try
            {
                _params ??= new HttpCustomerPostParams();
                string _name = Clean(_params.Name);
                string _email = _params.Email.NullIfWhiteSpace();
                string _phone = _params.Phone.NullIfWhiteSpace();
                string _gstin = _params.Gstin.NullIfWhiteSpace()?.ToUpperInvariant();

                List<string> _errors = Validate(_params, _name, _email, _phone, _gstin);
                if (_errors.Count > 0)
                    return (null, new FunctionReturn { Status = false, MessageType = Constants.ReturnMessageTypes.ValidationError, Message = _errors, HttpStatusCode = Constants.HttpStatusCodes.BadRequest, MethodName = _methodName });

                Guid _id;
                try
                {
                    // The procedure refuses duplicates (same GSTIN, or same name in the same city) under one lock.
                    _id = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.CustomerCreate, new[]
                    {
                        P("@Name", _name), P("@ContactPerson", Clean(_params.ContactPerson)), P("@Email", _email), P("@Phone", _phone), P("@GSTIN", _gstin),
                        P("@Address", Clean(_params.Address)), P("@City", Clean(_params.City)), P("@State", Clean(_params.State)),
                        P("@Country", Clean(_params.Country) ?? "India"), P("@SalesUserId", _user.Id)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                CustomerDto _created = await GetRowAsync(_id, _ct);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "Create", "Customer", _id, _user,
                    new { _created.Name, _created.Gstin, _created.City }, _ipAddress, _ct);
                return (_created, new FunctionReturn { MethodName = _methodName, HttpStatusCode = Constants.HttpStatusCodes.Created, Message = new List<string> { Msg.CustomerCreated } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
