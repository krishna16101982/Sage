using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageCommonLib.Models.Paging;
using SageCommonLib.Security;
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
    /// <summary>
    /// Recording, listing and cancelling sales. Visibility: a sales user sees their own sales; a sales manager also sees the
    /// sales of the people who reported to them when the sale was made. Totals are always recalculated by usp_Sale_Create.
    /// </summary>
    public class SaleFunctions : ISaleFunctions
    {
        private static readonly string[] SortKeys = { "soldAt", "saleNumber", "customer", "product", "saleAmount", "renewalDate" };


        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly ICatalogFunctions _catalogFunctions;

        private readonly AuthSettings _authSettings;

        public SaleFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, ICatalogFunctions __catalogFunctions, IOptions<AuthSettings> __authSettings)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _catalogFunctions = __catalogFunctions;
            _authSettings = __authSettings.Value;
        }

        #region Helpers

        private static bool HasMoreThanTwoDecimals(decimal? _value) => _value.HasValue && decimal.Round(_value.Value, 2) != _value.Value;

        /// <summary>One sale row (from GetRowAsync or dbo.usp_Sale_GetPaged; both use these column names).</summary>
        private static SaleDto MapSale(DataRow _row)
        {
            decimal _list = _row.GetDecimal("ListAmount"), _sale = _row.GetDecimal("SaleAmount");
            decimal _discount = _row.GetDecimal("DiscountAmount"), _cost = _row.GetDecimal("CostAmount");
            return new SaleDto
            {
                Id = _row.GetGuid("PublicId"),
                SaleNumber = _row.GetText("SaleNumber"),
                CustomerId = _row.GetGuid("CustomerPublicId"),
                CustomerName = _row.GetText("CustomerName"),
                CategoryName = _row.GetText("CategoryName"),
                ProductName = _row.GetText("ProductName"),
                PlanName = _row.GetText("PlanName"),
                SellerId = _row.GetGuid("SellerPublicId"),
                SellerName = _row.GetText("SellerName"),
                Quantity = _row.GetInt("Quantity"),
                UnitPrice = _row.GetDecimal("UnitPrice"),
                UnitCost = _row.GetDecimal("UnitCost"),
                ListAmount = _list,
                SaleAmount = _sale,
                DiscountAmount = _discount,
                DiscountPercent = _list > 0 ? decimal.Round(_discount * 100m / _list, 2) : 0m,
                CostAmount = _cost,
                Margin = _sale - _cost,
                DurationUnit = _row.GetText("DurationUnit"),
                DurationCount = _row.GetInt("DurationCount"),
                StartDate = _row.GetDate("StartDate"),
                RenewalDate = _row.GetDate("RenewalDate"),
                Status = _row.GetText("Status"),
                Notes = _row.GetText("Notes"),
                CreatedAtUtc = _row.GetUtc("CreatedAtUtc"),
                CancelledAtUtc = _row.GetNullableUtc("CancelledAtUtc"),
                CancelledByName = _row.GetText("CancelledByName"),
                CancelReason = _row.GetText("CancelReason"),
                HasPendingCancelRequest = _row.GetBool("HasPendingCancelRequest")
            };
        }

        /// <summary>One sale the user may see (their own, or their team's for a manager), or null.</summary>
        private async Task<SaleDto> GetRowAsync(Guid _id, long _userId, CancellationToken _ct)
        {
            // Same column names as dbo.usp_Sale_GetPaged, so MapSale reads both.
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT s.PublicId, s.SaleNumber, cu.PublicId AS CustomerPublicId, cu.Name AS CustomerName, c.Name AS CategoryName, ");
            _sqlQuery.Append("        p.Name AS ProductName, pl.Name AS PlanName, ");
            _sqlQuery.Append("        su.PublicId AS SellerPublicId, CONCAT(su.FirstName, N' ', su.LastName) AS SellerName, ");
            _sqlQuery.Append("        s.Quantity, s.UnitPrice, s.UnitCost, s.ListAmount, s.SaleAmount, s.DiscountAmount, s.CostAmount, ");
            _sqlQuery.Append("        s.DurationUnit, s.DurationCount, s.StartDate, s.RenewalDate, s.Status, s.Notes, s.CreatedAtUtc, s.CancelledAtUtc, ");
            _sqlQuery.Append("        CASE WHEN cb.Id IS NULL THEN NULL ELSE CONCAT(cb.FirstName, N' ', cb.LastName) END AS CancelledByName, s.CancelReason, ");
            _sqlQuery.Append("        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.SaleCancelRequests r WHERE r.SaleId = s.Id AND r.Status = 'Pending') ");
            _sqlQuery.Append("             THEN 1 ELSE 0 END AS BIT) AS HasPendingCancelRequest ");
            _sqlQuery.Append(" FROM dbo.Sales s ");
            _sqlQuery.Append(" JOIN dbo.Customers cu ON cu.Id = s.CustomerId ");
            _sqlQuery.Append(" JOIN dbo.ProductCategories c ON c.Id = s.CategoryId ");
            _sqlQuery.Append(" JOIN dbo.Products p ON p.Id = s.ProductId ");
            _sqlQuery.Append(" JOIN dbo.ProductPlans pl ON pl.Id = s.PlanId ");
            _sqlQuery.Append(" JOIN dbo.SalesUsers su ON su.Id = s.SalesUserId ");
            _sqlQuery.Append(" LEFT JOIN dbo.SalesUsers cb ON cb.Id = s.CancelledBySalesUserId ");
            _sqlQuery.Append(" WHERE s.PublicId = @id AND (s.SalesUserId = @u OR s.ManagerId = @u) ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@id", _id), P("@u", _userId) }, "F:Sale:GetRow", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            return _row == null ? null : MapSale(_row);
        }

        /// <summary>A manager's team: each member with their cancel permission and pending cancel requests.</summary>
        private async Task<List<TeamMember>> ReadTeamAsync(long _managerId, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT u.PublicId, CONCAT(u.FirstName, N' ', u.LastName) AS Name, u.Username, u.Status, u.CanRequestCancellation, ");
            _sqlQuery.Append("        (SELECT COUNT(*) FROM dbo.SaleCancelRequests r JOIN dbo.Sales s ON s.Id = r.SaleId ");
            _sqlQuery.Append("         WHERE r.RequestedBySalesUserId = u.Id AND r.Status = 'Pending' AND s.ManagerId = @u) AS PendingRequests ");
            _sqlQuery.Append(" FROM dbo.SalesUsers u ");
            _sqlQuery.Append(" WHERE u.ManagerId = @u AND u.IsDeleted = 0 ");
            _sqlQuery.Append(" ORDER BY u.FirstName, u.LastName ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@u", _managerId) }, "F:Sale:ReadTeam", _ct);
            EnsureRead(_functionReturn);
            return _dataTable.MapRows(_row => new TeamMember
            {
                Id = _row.GetGuid("PublicId"),
                Name = _row.GetText("Name"),
                Username = _row.GetText("Username"),
                Status = _row.GetText("Status"),
                CanRequestCancellation = _row.GetBool("CanRequestCancellation"),
                PendingRequests = _row.GetInt("PendingRequests")
            });
        }

        /// <summary>
        /// Cancellation requests (newest 200). '_where' and '_orderBy' are fixed strings from this class, never user input;
        /// @status (optional) is always a parameter.
        /// </summary>
        private async Task<List<CancelRequestDto>> ReadRequestsAsync(string _where, string _orderBy, IEnumerable<SqlParameter> _parameters, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT TOP 200 r.PublicId, s.PublicId AS SalePublicId, s.SaleNumber, cu.Name AS CustomerName, p.Name AS ProductName, s.SaleAmount, ");
            _sqlQuery.Append("        rb.PublicId AS RequestedByPublicId, CONCAT(rb.FirstName, N' ', rb.LastName) AS RequestedByName, ");
            _sqlQuery.Append("        r.Reason, r.Status, r.RequestedAtUtc, ");
            _sqlQuery.Append("        CASE WHEN db.Id IS NULL THEN NULL ELSE CONCAT(db.FirstName, N' ', db.LastName) END AS DecidedByName, ");
            _sqlQuery.Append("        r.DecidedAtUtc, r.DecisionNote ");
            _sqlQuery.Append(" FROM dbo.SaleCancelRequests r ");
            _sqlQuery.Append(" JOIN dbo.Sales s ON s.Id = r.SaleId ");
            _sqlQuery.Append(" JOIN dbo.Customers cu ON cu.Id = s.CustomerId ");
            _sqlQuery.Append(" JOIN dbo.Products p ON p.Id = s.ProductId ");
            _sqlQuery.Append(" JOIN dbo.SalesUsers rb ON rb.Id = r.RequestedBySalesUserId ");
            _sqlQuery.Append(" LEFT JOIN dbo.SalesUsers db ON db.Id = r.DecidedBySalesUserId ");
            _sqlQuery.Append(" WHERE (@status IS NULL OR r.Status = @status) AND " + _where);
            _sqlQuery.Append(" ORDER BY " + _orderBy);

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), _parameters, "F:Sale:ReadRequests", _ct);
            EnsureRead(_functionReturn);
            return _dataTable.MapRows(MapRequest);
        }

        private static IFunctionReturn Invalid(List<string> _errors, string _methodName)
            => new FunctionReturn { Status = false, MessageType = Constants.ReturnMessageTypes.ValidationError, Message = _errors, HttpStatusCode = Constants.HttpStatusCodes.BadRequest, MethodName = _methodName };

        private static IFunctionReturn ManagersOnly(string _methodName, string _message = Msg.ManagersOnly)
            => FunctionReturn.Fail(Constants.ReturnMessageTypes.PermissionDenied, _message, Constants.HttpStatusCodes.Forbidden, _methodName);

        private static CancelRequestDto MapRequest(DataRow _row) => new CancelRequestDto
        {
            Id = _row.GetGuid("PublicId"),
            SaleId = _row.GetGuid("SalePublicId"),
            SaleNumber = _row.GetText("SaleNumber"),
            CustomerName = _row.GetText("CustomerName"),
            ProductName = _row.GetText("ProductName"),
            SaleAmount = _row.GetDecimal("SaleAmount"),
            RequestedById = _row.GetGuid("RequestedByPublicId"),
            RequestedByName = _row.GetText("RequestedByName"),
            Reason = _row.GetText("Reason"),
            Status = _row.GetText("Status"),
            RequestedAtUtc = _row.GetUtc("RequestedAtUtc"),
            DecidedByName = _row.GetText("DecidedByName"),
            DecidedAtUtc = _row.GetNullableUtc("DecidedAtUtc"),
            DecisionNote = _row.GetText("DecisionNote")
        };

        /// <summary>Reason / note text: trimmed, tags removed, at most 500 characters.</summary>
        private static string CleanText(string _value) => string.IsNullOrWhiteSpace(_value) ? null : Sanitization.Sanitize(_value.Trim());

        #endregion

        public async Task<(SalesPage result, IFunctionReturn functionReturn)> GetSalesAsync(SaleFilter _filter, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:GetSales";
            try
            {
                _filter ??= new SaleFilter();
                bool _team = string.Equals(_filter.Scope, ApplicationDatabaseConstants.SaleScope.Team, StringComparison.OrdinalIgnoreCase);
                if (_team && !_user.IsManager) return (null, ManagersOnly(_methodName));

                PaginationParams _paging = new Pagination().GetPagingParams(_filter, SortKeys, "soldAt");
                string _status = _filter.Status == ApplicationDatabaseConstants.SaleStatus.Active || _filter.Status == ApplicationDatabaseConstants.SaleStatus.Cancelled ? _filter.Status : null;
                string _search = string.IsNullOrWhiteSpace(_filter.Search) ? null : "%" + _filter.Search.Trim().EscapeLike() + "%";
                var _totalCount = IntOut("@TotalCount");
                var _totalSale = MoneyOut("@TotalSaleAmount");
                var _totalMargin = MoneyOut("@TotalMargin");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteProcedureDataTableAsync(SP.SaleGetPaged, new[]
                {
                    P("@SalesUserId", _user.Id), P("@Scope", _team ? ApplicationDatabaseConstants.SaleScope.Team : ApplicationDatabaseConstants.SaleScope.Mine),
                    P("@SellerPublicId", _team ? _filter.SellerId : null), P("@Search", _search), P("@Status", _status),
                    Date("@SoldFrom", _filter.SoldFrom), Date("@SoldTo", _filter.SoldTo),
                    P("@SortBy", _paging.SortBy), P("@SortDirection", _paging.SortDirection), P("@Offset", _paging.Offset), P("@PageSize", _paging.PageSize),
                    _totalCount, _totalSale, _totalMargin
                }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                return (new SalesPage
                {
                    Items = _dataTable.MapRows(MapSale),
                    Page = _paging.Page,
                    PageSize = _paging.PageSize,
                    TotalCount = OutInt(_totalCount),
                    TotalSaleAmount = OutMoney(_totalSale),
                    TotalMargin = OutMoney(_totalMargin)
                }, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SaleDto sale, IFunctionReturn functionReturn)> GetSaleAsync(Guid _id, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:GetSale";
            try
            {
                SaleDto _sale = await GetRowAsync(_id, _user.Id, _ct);
                if (_sale == null) return (null, ApplicationCommonFunctions.NotFound(Msg.SaleNotFound, _methodName));
                _sale.CancelRequests = await ReadRequestsAsync(" s.PublicId = @sale ", " r.RequestedAtUtc DESC ", new[] { P("@status", null), P("@sale", _id) }, _ct);
                return (_sale, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SaleDto sale, IFunctionReturn functionReturn)> CreateSaleAsync(HttpSalePostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:CreateSale";
            try
            {
                _params ??= new HttpSalePostParams();
                var _errors = new List<string>();
                if (!_params.CustomerId.HasValue || _params.CustomerId == Guid.Empty) _errors.Add(Msg.CustomerRequired);
                if (_params.PlanId <= 0) _errors.Add(Msg.PlanRequired);
                DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (!_params.StartDate.HasValue) _errors.Add(Msg.StartDateRequired);
                else
                {
                    // Backdating limit from Admin Panel > Settings; the start date may be at most a year ahead.
                    int _maxBack = (await _catalogFunctions.GetSalesRulesAsync(_ct)).MaxBackdateDays;
                    if (_params.StartDate.Value < _today.AddDays(-_maxBack))
                        _errors.Add(_maxBack == 0 ? "The start date cannot be in the past (company setting)." : $"The start date cannot be more than {_maxBack} days in the past (company setting).");
                    else if (_params.StartDate.Value > _today.AddYears(1)) _errors.Add(Msg.StartDateTooFarAhead);
                }
                if (HasMoreThanTwoDecimals(_params.SaleAmount) || HasMoreThanTwoDecimals(_params.DiscountAmount)) _errors.Add(Msg.AmountDecimals);
                string _notes = string.IsNullOrWhiteSpace(_params.Notes) ? null : Sanitization.Sanitize(_params.Notes.Trim());
                if (_notes != null && _notes.Length > 1000) _errors.Add(Msg.NotesTooLong);
                if (_errors.Count > 0) return (null, Invalid(_errors, _methodName));

                // Quantity, amounts, duration and renewal date are checked by the procedure against the price list.
                Guid _id;
                try
                {
                    _id = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.SaleCreate, new[]
                    {
                        P("@SalesUserId", _user.Id), P("@CustomerPublicId", _params.CustomerId), P("@PlanId", _params.PlanId), P("@Quantity", _params.Quantity),
                        Money("@SaleAmount", _params.SaleAmount), Money("@DiscountAmount", _params.DiscountAmount),
                        P("@DurationUnit", _params.DurationUnit.NullIfWhiteSpace()), P("@DurationCount", _params.DurationCount),
                        Date("@StartDate", _params.StartDate), Date("@RenewalDate", _params.RenewalDate), P("@Notes", _notes)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                SaleDto _created = await GetRowAsync(_id, _user.Id, _ct);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "Create", "Sale", _id, _user, new
                {
                    _created.SaleNumber, _created.CustomerName, _created.ProductName, _created.PlanName, _created.Quantity,
                    _created.UnitPrice, _created.ListAmount, _created.SaleAmount, _created.DiscountAmount, _created.StartDate, _created.RenewalDate
                }, _ipAddress, _ct);
                return (_created, new FunctionReturn { MethodName = _methodName, HttpStatusCode = Constants.HttpStatusCodes.Created, Message = new List<string> { Msg.SaleCreated } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SaleDto sale, IFunctionReturn functionReturn)> CancelSaleAsync(Guid _id, HttpSaleCancelParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:CancelSale";
            try
            {
                // Sales persons ask their manager instead (CreateCancelRequestAsync).
                if (!_user.IsManager) return (null, ManagersOnly(_methodName, Msg.ManagersOnlyCancel));
                string _reason = CleanText(_params?.Reason);
                if (string.IsNullOrEmpty(_reason) || _reason.Length > 500)
                    return (null, ApplicationCommonFunctions.BadRequest(Msg.CancelReasonRequired, _methodName));

                // The procedure allows the sale's manager, or a manager for their own sale (404 for anyone else).
                (IFunctionReturn _functionReturn, long _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SaleCancel, new[]
                {
                    P("@SalePublicId", _id), P("@SalesUserId", _user.Id), P("@Reason", _reason)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                SaleDto _cancelled = await GetRowAsync(_id, _user.Id, _ct);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "Cancel", "Sale", _id, _user, new { _cancelled.SaleNumber, Reason = _reason }, _ipAddress, _ct);
                return (_cancelled, new FunctionReturn { MethodName = _methodName, Message = new List<string> { Msg.SaleCancelled } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(List<TeamMember> members, IFunctionReturn functionReturn)> GetTeamMembersAsync(SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:GetTeamMembers";
            try
            {
                if (!_user.IsManager) return (null, ManagersOnly(_methodName));
                List<TeamMember> _members = await ReadTeamAsync(_user.Id, _ct);
                return (_members, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(TeamMember member, IFunctionReturn functionReturn)> SetTeamMemberStatusAsync(Guid _memberId, HttpTeamMemberStatusParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:SetTeamMemberStatus";
            try
            {
                if (!_user.IsManager) return (null, ManagersOnly(_methodName, Msg.ManagersOnlyTeam));
                string _status = _params?.Status?.Trim();
                if (_status != "Active" && _status != "Inactive") return (null, ApplicationCommonFunctions.BadRequest("Status must be Active or Inactive.", _methodName));

                // Only the manager's own team.
                TeamMember _before = (await ReadTeamAsync(_user.Id, _ct)).FirstOrDefault(m => m.Id == _memberId);
                if (_before == null) return (null, ApplicationCommonFunctions.NotFound("This person is not in your team.", _methodName));

                if (_before.Status != _status)
                {
                    (IFunctionReturn _functionReturn, long _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SalesUserSetStatus, new[]
                    {
                        P("@PublicId", _memberId), P("@Status", _status), P("@ActorUserId", null)
                    }, _methodName, null, _ct);
                    if (!_functionReturn.Status) return (null, _functionReturn);
                    await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "TeamMemberStatus", "SalesUser", _memberId, _user,
                        new { From = _before.Status, To = _status }, _ipAddress, _ct);
                }

                TeamMember _member = (await ReadTeamAsync(_user.Id, _ct)).FirstOrDefault(m => m.Id == _memberId);
                string _message = _status == "Active" ? $"{_member?.Name} is active again." : $"{_member?.Name} is deactivated and signed out.";
                return (_member, new FunctionReturn { MethodName = _methodName, Message = new List<string> { _message } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(TeamMember member, IFunctionReturn functionReturn)> CreateTeamMemberAsync(HttpTeamMemberPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:CreateTeamMember";
            try
            {
                if (!_user.IsManager) return (null, ManagersOnly(_methodName));

                // Same rules as Admin Panel > Sales users > Add (names, username, email, mobile, password strength).
                var _errors = new List<string>();
                string _first = CleanText(_params?.FirstName), _last = CleanText(_params?.LastName);
                string _username = _params?.Username?.Trim(), _email = _params?.Email?.Trim(), _mobile = CleanText(_params?.MobileNumber);
                if (string.IsNullOrEmpty(_first) || _first.Length > 100) _errors.Add("Enter the first name (up to 100 characters).");
                if (string.IsNullOrEmpty(_last) || _last.Length > 100) _errors.Add("Enter the last name (up to 100 characters).");
                if (string.IsNullOrEmpty(_username)) _errors.Add("Enter a username.");
                else if (!ValidationFunctions.IsValidUsername(_username)) _errors.Add("The username may use letters, numbers, dot, dash and underscore (3 to 50 characters).");
                if (!ValidationFunctions.IsValidEmail(_email) || _email.Length > 256) _errors.Add("Enter a valid email address.");
                if (_mobile != null && !ValidationFunctions.IsValidPhone(_mobile)) _errors.Add("Enter a 10-digit mobile number.");
                _errors.AddRange(ValidationFunctions.CheckPasswordStrength(_params?.Password));
                if (_params?.Password != _params?.ConfirmPassword) _errors.Add("The passwords do not match.");
                if (_errors.Count > 0) return (null, Invalid(_errors, _methodName));

                // The procedure checks duplicate username / email and that the manager is an active sales manager.
                Guid _id;
                try
                {
                    _id = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.SalesUserCreate, new[]
                    {
                        P("@FirstName", _first), P("@LastName", _last), P("@Username", _username), P("@Email", _email), P("@MobileNumber", _mobile),
                        P("@PasswordHash", SecurityHash.HashPassword(_params.Password, _authSettings.PasswordHashIterations)),
                        P("@Role", "SalesPerson"), P("@ManagerPublicId", _user.PublicId), P("@ActorUserId", null)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "CreateTeamMember", "SalesUser", _id, _user,
                    new { FirstName = _first, LastName = _last, Username = _username, Email = _email, MobileNumber = _mobile, Role = "SalesPerson" }, _ipAddress, _ct);
                TeamMember _member = (await ReadTeamAsync(_user.Id, _ct)).FirstOrDefault(m => m.Id == _id);
                return (_member, new FunctionReturn { MethodName = _methodName, HttpStatusCode = Constants.HttpStatusCodes.Created, Message = new List<string> { $"{_first} {_last} added to your team." } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(CancelRequestDto request, IFunctionReturn functionReturn)> CreateCancelRequestAsync(Guid _saleId, HttpSaleCancelParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:CreateCancelRequest";
            try
            {
                if (_user.IsManager) return (null, ApplicationCommonFunctions.BadRequest("Managers cancel sales directly.", _methodName));
                if (!_user.CanRequestCancellation) return (null, ManagersOnly(_methodName, Msg.RequestNotAllowed));
                string _reason = CleanText(_params?.Reason);
                if (string.IsNullOrEmpty(_reason) || _reason.Length > 500) return (null, ApplicationCommonFunctions.BadRequest(Msg.ReasonRequired, _methodName));

                Guid _requestId;
                try
                {
                    _requestId = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.SaleCancelRequestCreate, new[]
                    {
                        P("@SalePublicId", _saleId), P("@SalesUserId", _user.Id), P("@Reason", _reason)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                CancelRequestDto _request = (await ReadRequestsAsync(" r.PublicId = @id ", " r.RequestedAtUtc DESC ",
                    new[] { P("@status", null), P("@id", _requestId) }, _ct)).FirstOrDefault();
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "CancelRequest", "Sale", _saleId, _user, new { _request.SaleNumber, Reason = _reason }, _ipAddress, _ct);
                return (_request, new FunctionReturn { MethodName = _methodName, HttpStatusCode = Constants.HttpStatusCodes.Created, Message = new List<string> { Msg.CancelRequested } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(List<CancelRequestDto> requests, IFunctionReturn functionReturn)> GetCancelRequestsAsync(string _status, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:GetCancelRequests";
            try
            {
                string _filter = _status == "Pending" || _status == "Approved" || _status == "Rejected" ? _status : null;
                // A manager sees the requests on their team's sales; a sales person sees their own requests.
                string _scope = _user.IsManager ? " s.ManagerId = @u " : " r.RequestedBySalesUserId = @u ";
                List<CancelRequestDto> _requests = await ReadRequestsAsync(_scope, " CASE WHEN r.Status = 'Pending' THEN 0 ELSE 1 END, r.RequestedAtUtc DESC ",
                    new[] { P("@status", _filter), P("@u", _user.Id) }, _ct);
                return (_requests, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(CancelRequestDto request, IFunctionReturn functionReturn)> DecideCancelRequestAsync(Guid _requestId, bool _approve, HttpCancelDecisionParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:DecideCancelRequest";
            try
            {
                if (!_user.IsManager) return (null, ManagersOnly(_methodName, Msg.ManagersOnlyTeam));
                string _note = CleanText(_params?.Note);
                if (_note != null && _note.Length > 500) return (null, ApplicationCommonFunctions.BadRequest(Msg.NoteTooLong, _methodName));

                Guid _saleId;
                try
                {
                    _saleId = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.SaleCancelRequestDecide, new[]
                    {
                        P("@RequestPublicId", _requestId), P("@ManagerId", _user.Id), P("@Approve", _approve), P("@Note", _note)
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                CancelRequestDto _request = (await ReadRequestsAsync(" r.PublicId = @id ", " r.RequestedAtUtc DESC ",
                    new[] { P("@status", null), P("@id", _requestId) }, _ct)).FirstOrDefault();
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, _approve ? "CancelRequestApprove" : "CancelRequestReject", "Sale", _saleId, _user,
                    new { _request.SaleNumber, _request.RequestedByName, _request.Reason, Note = _note }, _ipAddress, _ct);
                return (_request, new FunctionReturn { MethodName = _methodName, Message = new List<string> { _approve ? Msg.RequestApproved : Msg.RequestRejected } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(TeamMember member, IFunctionReturn functionReturn)> SetCancelPermissionAsync(Guid _memberId, HttpCancelPermissionParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Sale:SetCancelPermission";
            try
            {
                if (!_user.IsManager) return (null, ManagersOnly(_methodName, Msg.ManagersOnlyTeam));
                bool _allowed = _params?.CanRequestCancellation ?? false;
                (IFunctionReturn _functionReturn, long _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SalesUserSetCancelPermission, new[]
                {
                    P("@ManagerId", _user.Id), P("@MemberPublicId", _memberId), P("@CanRequestCancellation", _allowed)
                }, _methodName, null, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                TeamMember _member = (await ReadTeamAsync(_user.Id, _ct)).FirstOrDefault(m => m.Id == _memberId);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "CancelPermission", "SalesUser", _memberId, _user, new { CanRequestCancellation = _allowed }, _ipAddress, _ct);
                return (_member, new FunctionReturn { MethodName = _methodName, Message = new List<string> { Msg.PermissionSaved } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
