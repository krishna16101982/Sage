using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.AppValidations;
using SageUserApi.Models;
using SageUserApi.Models.HttpPost;
using SP = SageUserApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using Msg = SageUserApi.Globals.ApplicationConstants.Messages;
using static SageCommonLib.AppFunctions.Sql;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// The "New order form" (User Panel): its lists and defaults, saving an order (a sale in dbo.Sales plus GST, free
    /// months, billing / payment terms, contacts and bill-to address), and reading those details back. The simple
    /// "New sale" page (SaleFunctions.CreateSaleAsync) is not changed by any of this.
    /// </summary>
    public class OrderFormFunctions : IOrderFormFunctions
    {
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly ICatalogFunctions _catalogFunctions;
        private readonly ISaleFunctions _saleFunctions;

        public OrderFormFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, ICatalogFunctions __catalogFunctions, ISaleFunctions __saleFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _catalogFunctions = __catalogFunctions;
            _saleFunctions = __saleFunctions;
        }

        #region Helpers

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        private const int MaxWordingLength = 4000;

        /// <summary>Settings key of each standard wording section.</summary>
        private static readonly (string Key, Action<OrderWording, string> Set)[] WordingSettings =
        {
            ("OrderForm.SignReturnLine", (w, v) => w.SignReturnLine = v),
            ("OrderForm.SignReturnInstruction", (w, v) => w.SignReturnInstruction = v),
            ("OrderForm.OrderNotes", (w, v) => w.OrderNotes = v),
            ("OrderForm.FreeMonthNote", (w, v) => w.FreeMonthNote = v),
            ("OrderForm.TaxInformation", (w, v) => w.TaxInformation = v),
            ("OrderForm.SignatureClauses", (w, v) => w.SignatureClauses = v),
            ("OrderForm.Footer", (w, v) => w.Footer = v)
        };

        /// <summary>Trims each section, strips tags and checks its length; null when nothing is left.</summary>
        private static OrderWording CleanWording(OrderWording _wording, List<string> _errors)
        {
            if (_wording == null) return null;
            string Section(string _value, string _label)
            {
                string _clean = Clean(_value?.Replace("\r\n", "\n"));
                if (_clean != null && _clean.Length > MaxWordingLength) _errors.Add($"{_label} must be {MaxWordingLength} characters or fewer.");
                return _clean;
            }
            var _result = new OrderWording
            {
                SignReturnLine = Section(_wording.SignReturnLine, "Sign-and-return line"),
                SignReturnInstruction = Section(_wording.SignReturnInstruction, "Sign-and-return instruction"),
                OrderNotes = Section(_wording.OrderNotes, "Order notes"),
                FreeMonthNote = Section(_wording.FreeMonthNote, "Free-month note"),
                ExtraItemTitle = Section(_wording.ExtraItemTitle, "Extra item title"),
                ExtraItemDescription = Section(_wording.ExtraItemDescription, "Extra item description"),
                ProductTitle = Section(_wording.ProductTitle, "Product title"),
                ProductDescription = Section(_wording.ProductDescription, "Product description"),
                PeriodLine = Section(_wording.PeriodLine, "Period line"),
                TaxInformation = Section(_wording.TaxInformation, "Tax information"),
                SignatureClauses = Section(_wording.SignatureClauses, "Signature clauses"),
                Footer = Section(_wording.Footer, "Footer")
            };
            return _result;
        }

        private static bool HasMoreThanTwoDecimals(decimal? _value) => _value.HasValue && decimal.Round(_value.Value, 2) != _value.Value;

        private static IFunctionReturn Invalid(List<string> _errors, string _methodName)
            => new FunctionReturn { Status = false, MessageType = Constants.ReturnMessageTypes.ValidationError, Message = _errors, HttpStatusCode = Constants.HttpStatusCodes.BadRequest, MethodName = _methodName };

        /// <summary>Trims a contact, removes tags, checks lengths and formats; adds problems to _errors. Null when empty.</summary>
        private static OrderContact CleanContact(OrderContactParams _contact, string _who, bool _nameRequired, List<string> _errors)
        {
            string _name = Clean(_contact?.Name), _designation = Clean(_contact?.Designation);
            string _email = _contact?.Email.NullIfWhiteSpace(), _phone = _contact?.Phone.NullIfWhiteSpace();
            if (_name == null && _nameRequired) _errors.Add($"Enter the {_who}'s name.");
            if (_name != null && _name.Length > 150) _errors.Add($"The {_who}'s name must be 150 characters or fewer.");
            if (_designation != null && _designation.Length > 100) _errors.Add($"The {_who}'s designation must be 100 characters or fewer.");
            if (_email != null && (!ValidationFunctions.IsValidEmail(_email) || _email.Length > 256)) _errors.Add($"Enter a valid email for the {_who}.");
            if (_phone != null && !ValidationFunctions.IsValidPhone(_phone)) _errors.Add($"Enter a valid contact number for the {_who}.");
            if (_name == null && _designation == null && _email == null && _phone == null) return null;
            return new OrderContact { Name = _name, Designation = _designation, Email = _email, Phone = _phone };
        }

        /// <summary>A number setting (Admin Panel > Settings), or the fallback when missing or unreadable.</summary>
        private static decimal Number(DataTable _settings, string _key, decimal _fallback)
        {
            foreach (DataRow _row in _settings.Rows)
                if (_row.GetText("SettingKey") == _key && decimal.TryParse(_row.GetText("SettingValue"), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal _value))
                    return _value;
            return _fallback;
        }

        #endregion

        public async Task<(OrderFormOptions options, IFunctionReturn functionReturn)> GetOptionsAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:OrderForm:GetOptions";
            try
            {
                // The four lists (active values, in order).
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT Id, MasterType, Code, Name, IsDefault FROM dbo.MasterValues ");
                _sqlQuery.Append(" WHERE IsActive = 1 ");
                _sqlQuery.Append(" ORDER BY MasterType, SortOrder, Name ");
                (IFunctionReturn _functionReturn, DataTable _masters) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), null, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                // Its two number settings.
                _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT SettingKey, SettingValue FROM dbo.Settings ");
                _sqlQuery.Append(" WHERE SettingKey IN ('Sales.DefaultGstPercent', 'Sales.MaxFreeMonths') OR SettingKey LIKE 'OrderForm.%' ");
                (_functionReturn, DataTable _settings) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), null, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                SalesRules _rules = await _catalogFunctions.GetSalesRulesAsync(_ct);
                var _options = new OrderFormOptions
                {
                    DefaultGstPercent = Math.Clamp(Number(_settings, "Sales.DefaultGstPercent", 18), 0, 28),
                    MaxFreeMonths = (int)Math.Clamp(Number(_settings, "Sales.MaxFreeMonths", 12), 0, 24),
                    MaxDiscountPercent = _rules.MaxDiscountPercent,
                    MaxBackdateDays = _rules.MaxBackdateDays
                };
                foreach ((string _key, Action<OrderWording, string> _set) in WordingSettings)
                    foreach (DataRow _row in _settings.Rows)
                        if (_row.GetText("SettingKey") == _key) _set(_options.Wording, _row.GetText("SettingValue"));
                foreach (DataRow _row in _masters.Rows)
                {
                    var _option = new MasterOption { Id = _row.GetInt("Id"), Code = _row.GetText("Code"), Name = _row.GetText("Name"), IsDefault = _row.GetBool("IsDefault") };
                    switch (_row.GetText("MasterType"))
                    {
                        case "BillingFrequency": _options.BillingFrequencies.Add(_option); break;
                        case "BillingMethod": _options.BillingMethods.Add(_option); break;
                        case "PaymentMethod": _options.PaymentMethods.Add(_option); break;
                        case "PaymentTerm": _options.PaymentTerms.Add(_option); break;
                        case "Currency": _options.Currencies.Add(_option); break;
                    }
                }
                return (_options, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SaleDto sale, IFunctionReturn functionReturn)> CreateOrderAsync(HttpOrderFormPostParams _params, SalesUserContext _user, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:OrderForm:CreateOrder";
            try
            {
                _params ??= new HttpOrderFormPostParams();
                var _errors = new List<string>();

                // The sale: same checks as the simple "New sale" page.
                if (!_params.CustomerId.HasValue || _params.CustomerId == Guid.Empty) _errors.Add(Msg.CustomerRequired);
                if (_params.PlanId <= 0) _errors.Add(Msg.PlanRequired);
                DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (!_params.StartDate.HasValue) _errors.Add(Msg.StartDateRequired);
                else
                {
                    int _maxBack = (await _catalogFunctions.GetSalesRulesAsync(_ct)).MaxBackdateDays;
                    if (_params.StartDate.Value < _today.AddDays(-_maxBack))
                        _errors.Add(_maxBack == 0 ? "The start date cannot be in the past (company setting)." : $"The start date cannot be more than {_maxBack} days in the past (company setting).");
                    else if (_params.StartDate.Value > _today.AddYears(1)) _errors.Add(Msg.StartDateTooFarAhead);
                }
                if (HasMoreThanTwoDecimals(_params.SaleAmount) || HasMoreThanTwoDecimals(_params.DiscountAmount)) _errors.Add(Msg.AmountDecimals);
                string _notes = Clean(_params.Notes);
                if (_notes != null && _notes.Length > 1000) _errors.Add(Msg.NotesTooLong);

                // The order form.
                if (!_params.GstPercent.HasValue || _params.GstPercent < 0 || _params.GstPercent > 28 || HasMoreThanTwoDecimals(_params.GstPercent))
                    _errors.Add("GST must be between 0 and 28 percent.");
                if (_params.BillingFrequencyId <= 0) _errors.Add("Choose a billing frequency.");
                if (_params.BillingMethodId <= 0) _errors.Add("Choose a billing method.");
                if (_params.PaymentMethodId <= 0) _errors.Add("Choose a payment method.");
                if (_params.PaymentTermId <= 0) _errors.Add("Choose a payment term.");
                if (_params.CurrencyId <= 0) _errors.Add("Choose a currency.");
                OrderContact _primary = CleanContact(_params.PrimaryContact, "primary contact", true, _errors);
                OrderContact _billing = _params.BillingSameAsPrimary ? _primary : CleanContact(_params.BillingContact, "billing contact", false, _errors) ?? _primary;
                string _address = Clean(_params.BillToAddress);
                if (_address != null && _address.Length > 500) _errors.Add("The bill-to address must be 500 characters or fewer.");
                OrderWording _wording = CleanWording(_params.Wording, _errors);
                // The free-month note only belongs on an order with free months.
                if (_wording != null && _params.FreeMonths <= 0) _wording.FreeMonthNote = null;
                if (_errors.Count > 0) return (null, Invalid(_errors, _methodName));

                // Free months, master values, discount limit, quantity, duration and renewal date are checked by the procedure.
                IFunctionReturn _functionReturn;
                Guid _id;
                try
                {
                    _id = await _databaseFunctions.ExecuteProcedureScalarAsync<Guid>(SP.SaleOrderCreate, new[]
                    {
                        P("@SalesUserId", _user.Id), P("@CustomerPublicId", _params.CustomerId), P("@PlanId", _params.PlanId), P("@Quantity", _params.Quantity),
                        Money("@SaleAmount", _params.SaleAmount), Money("@DiscountAmount", _params.DiscountAmount),
                        P("@DurationUnit", _params.DurationUnit.NullIfWhiteSpace()), P("@DurationCount", _params.DurationCount),
                        Date("@StartDate", _params.StartDate), Date("@RenewalDate", _params.RenewalDate), P("@Notes", _notes),
                        Money("@GstPercent", _params.GstPercent), P("@FreeMonths", _params.FreeMonths),
                        P("@BillingFrequencyId", _params.BillingFrequencyId), P("@BillingMethodId", _params.BillingMethodId),
                        P("@PaymentMethodId", _params.PaymentMethodId), P("@PaymentTermId", _params.PaymentTermId), P("@CurrencyId", _params.CurrencyId),
                        P("@PrimaryContactName", _primary?.Name), P("@PrimaryContactDesignation", _primary?.Designation),
                        P("@PrimaryContactEmail", _primary?.Email), P("@PrimaryContactPhone", _primary?.Phone),
                        P("@BillingContactName", _billing?.Name), P("@BillingContactDesignation", _billing?.Designation),
                        P("@BillingContactEmail", _billing?.Email), P("@BillingContactPhone", _billing?.Phone),
                        P("@BillToAddress", _address),
                        P("@OrderWording", _wording == null ? null : JsonSerializer.Serialize(_wording, JsonOptions))
                    }, _ct);
                }
                catch (AppException ex)
                {
                    return (null, FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, _methodName));
                }

                (SaleDto _created, _functionReturn) = await _saleFunctions.GetSaleAsync(_id, _user, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                (OrderDetailsDto _details, _) = await GetOrderDetailsAsync(_id, _user, _ct);
                await SalesAudit.LogAsync(_databaseFunctions, _loggerFunctions, "CreateOrder", "Sale", _id, _user, new
                {
                    _created.SaleNumber, _created.CustomerName, _created.ProductName, _created.PlanName, _created.Quantity,
                    _created.ListAmount, _created.SaleAmount, _created.DiscountAmount, _details?.GstPercent, _details?.TaxAmount, _details?.TotalAmount,
                    _details?.FreeMonths, _created.StartDate, _created.RenewalDate, _details?.BillingFrequency, _details?.PaymentTerm
                }, _ipAddress, _ct);
                return (_created, new FunctionReturn { MethodName = _methodName, HttpStatusCode = Constants.HttpStatusCodes.Created, Message = new List<string> { "Order saved." } });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(OrderDetailsDto details, IFunctionReturn functionReturn)> GetOrderDetailsAsync(Guid _saleId, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:OrderForm:GetOrderDetails";
            try
            {
                // Same visibility as the sale page: the seller, or their manager.
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT s.PublicId, s.GstPercent, s.TaxAmount, s.TotalAmount, s.FreeMonths, ");
                _sqlQuery.Append("        bf.Name AS BillingFrequency, bm.Name AS BillingMethod, pm.Name AS PaymentMethod, pt.Name AS PaymentTerm, cy.Name AS Currency, ");
                _sqlQuery.Append("        s.PrimaryContactName, s.PrimaryContactDesignation, s.PrimaryContactEmail, s.PrimaryContactPhone, ");
                _sqlQuery.Append("        s.BillingContactName, s.BillingContactDesignation, s.BillingContactEmail, s.BillingContactPhone, s.BillToAddress, s.OrderWording, s.PriceFiscalYear, s.PriceSeatRange ");
                _sqlQuery.Append(" FROM dbo.Sales s ");
                _sqlQuery.Append(" LEFT JOIN dbo.MasterValues bf ON bf.Id = s.BillingFrequencyId ");
                _sqlQuery.Append(" LEFT JOIN dbo.MasterValues bm ON bm.Id = s.BillingMethodId ");
                _sqlQuery.Append(" LEFT JOIN dbo.MasterValues pm ON pm.Id = s.PaymentMethodId ");
                _sqlQuery.Append(" LEFT JOIN dbo.MasterValues pt ON pt.Id = s.PaymentTermId ");
                _sqlQuery.Append(" LEFT JOIN dbo.MasterValues cy ON cy.Id = s.CurrencyId ");
                _sqlQuery.Append(" WHERE s.PublicId = @id AND (s.SalesUserId = @u OR s.ManagerId = @u) ");

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                    _sqlQuery.ToString(), new[] { P("@id", _saleId), P("@u", _user.Id) }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                DataRow _row = _dataTable.FirstRow();
                if (_row == null) return (null, ApplicationCommonFunctions.NotFound(Msg.SaleNotFound, _methodName));
                return (MapDetails(_row), _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        private static OrderDetailsDto MapDetails(DataRow _row)
        {
            static OrderContact Contact(DataRow r, string _prefix)
            {
                var _contact = new OrderContact
                {
                    Name = r.GetText(_prefix + "Name"),
                    Designation = r.GetText(_prefix + "Designation"),
                    Email = r.GetText(_prefix + "Email"),
                    Phone = r.GetText(_prefix + "Phone")
                };
                return _contact.Name == null && _contact.Designation == null && _contact.Email == null && _contact.Phone == null ? null : _contact;
            }

            return new OrderDetailsDto
            {
                SaleId = _row.GetGuid("PublicId"),
                HasOrderForm = _row.GetText("BillingFrequency") != null,
                GstPercent = _row.GetDecimal("GstPercent"),
                TaxAmount = _row.GetDecimal("TaxAmount"),
                TotalAmount = _row.GetDecimal("TotalAmount"),
                FreeMonths = _row.GetInt("FreeMonths"),
                BillingFrequency = _row.GetText("BillingFrequency"),
                BillingMethod = _row.GetText("BillingMethod"),
                PaymentMethod = _row.GetText("PaymentMethod"),
                PaymentTerm = _row.GetText("PaymentTerm"),
                Currency = _row.GetText("Currency"),
                PrimaryContact = Contact(_row, "PrimaryContact"),
                BillingContact = Contact(_row, "BillingContact"),
                BillToAddress = _row.GetText("BillToAddress"),
                Wording = ReadWording(_row.GetText("OrderWording")),
                PriceFiscalYear = _row.GetNullableInt("PriceFiscalYear"),
                PriceSeatRange = _row.GetText("PriceSeatRange")
            };
        }

        /// <summary>The stored wording JSON, or null when missing or unreadable.</summary>
        private static OrderWording ReadWording(string _json)
        {
            if (string.IsNullOrEmpty(_json)) return null;
            try { return JsonSerializer.Deserialize<OrderWording>(_json, JsonOptions); }
            catch (JsonException) { return null; }
        }
    }
}
