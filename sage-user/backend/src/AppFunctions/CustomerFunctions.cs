using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageCommonLib.AppFunctions;
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
using static SageCommonLib.AppFunctions.Sql;

namespace SageUserApi.AppFunctions
{
    /// <summary>Customers are shared by all sales users: anyone can search them and add new ones.</summary>
    public partial class CustomerFunctions : ICustomerFunctions
    {
        private static readonly string[] SortKeys = { "name", "city", "createdAtUtc" };

        /// <summary>Same columns, in the same order, as dbo.usp_Customer_GetPaged.</summary>

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

        /// <summary>One customer row (from GetRowAsync or dbo.usp_Customer_GetPaged; both use these column names).</summary>
        private static CustomerDto MapCustomer(DataRow _row) => new CustomerDto
        {
            Id = _row.GetGuid("PublicId"),
            Name = _row.GetText("Name"),
            ContactPerson = _row.GetText("ContactPerson"),
            Email = _row.GetText("Email"),
            Phone = _row.GetText("Phone"),
            Gstin = _row.GetText("GSTIN"),
            Address = _row.GetText("Address"),
            City = _row.GetText("City"),
            State = _row.GetText("State"),
            Country = _row.GetText("Country"),
            CreatedAtUtc = _row.GetUtc("CreatedAtUtc"),
            CreatedByName = _row.GetText("CreatedByName"),
            SaleCount = _row.GetInt("SaleCount")
        };

        /// <summary>One active customer by PublicId, or null.</summary>
        private async Task<CustomerDto> GetRowAsync(Guid _id, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT c.PublicId, c.Name, c.ContactPerson, c.Email, c.Phone, c.GSTIN, c.Address, c.City, c.State, c.Country, c.CreatedAtUtc, ");
            _sqlQuery.Append("        CASE WHEN u.Id IS NOT NULL THEN CONCAT(u.FirstName, N' ', u.LastName) ");
            _sqlQuery.Append("             WHEN a.Id IS NOT NULL THEN CONCAT(a.FirstName, N' ', a.LastName, N' (Admin)') END AS CreatedByName, ");
            _sqlQuery.Append("        (SELECT COUNT(*) FROM dbo.Sales s WHERE s.CustomerId = c.Id AND s.Status = 'Active') AS SaleCount ");
            _sqlQuery.Append(" FROM dbo.Customers c ");
            _sqlQuery.Append(" LEFT JOIN dbo.SalesUsers u ON u.Id = c.CreatedBySalesUserId ");
            _sqlQuery.Append(" LEFT JOIN dbo.AdminUsers a ON a.Id = c.CreatedByAdminUserId ");
            _sqlQuery.Append(" WHERE c.PublicId = @id AND c.IsActive = 1 ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@id", _id) }, "F:Customer:GetRow", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            return _row == null ? null : MapCustomer(_row);
        }

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
                var _totalCount = IntOut("@TotalCount");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.CustomerGetPaged, new[]
                {
                    P("@Search", _search), P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection),
                    P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize), _totalCount
                }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                return (new PagedResult<CustomerDto>
                {
                    Items = _dataTable.MapRows(MapCustomer),
                    Page = _paging.Page,
                    PageSize = _paging.PageSize,
                    TotalCount = OutInt(_totalCount)
                }, _functionReturn);
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
