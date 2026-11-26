using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.AppValidations;
using SageUserApi.Models;
using SageUserApi.Models.HttpPost;
using static SageCommonLib.AppFunctions.Sql;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// The order form PDF of an order saved with the New order form. It is made when the order is saved and stored as
    /// {StoragePath}/{SaleNumber}/{SaleNumber}.pdf; an order without its file (e.g. saved before this feature, or the
    /// first save failed) gets it the first time it is opened. The Admin API reads the same folder.
    /// </summary>
    public class OrderFormPdfFunctions : IOrderFormPdfFunctions
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly OrderFormPdfSettings _settings;
        private readonly string _storage;
        private readonly string _assets;
        private readonly string _fonts;

        public OrderFormPdfFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IOptions<OrderFormPdfSettings> __settings, IWebHostEnvironment __env)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _settings = __settings.Value;
            _storage = OrderFormPdfSettings.Resolve(__env.ContentRootPath, _settings.StoragePath);
            _assets = OrderFormPdfSettings.Resolve(__env.ContentRootPath, _settings.AssetsPath);
            _fonts = OrderFormPdfSettings.Resolve(__env.ContentRootPath, _settings.FontsPath);
        }

        /// <summary>Makes and saves the PDF right after the order is saved. A failure is logged; the order stays saved.</summary>
        public async Task CreateAsync(Guid _saleId, CancellationToken _ct = default)
        {
            try
            {
                OrderFormPdfData _data = await ReadAsync(_saleId, null, _ct);
                if (_data != null) await SaveAsync(_data, _ct);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error("F:OrderFormPdf:Create", ex.Message, ex);
            }
        }

        public async Task<(OrderFormPdfFile file, IFunctionReturn functionReturn)> GetAsync(Guid _saleId, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:OrderFormPdf:Get";
            try
            {
                // Same visibility as the sale page: the seller, or their manager.
                OrderFormPdfData _data = await ReadAsync(_saleId, _user.Id, _ct);
                if (_data == null) return (null, ApplicationCommonFunctions.NotFound("Sale not found, or it was not saved with the order form.", _methodName));

                string _path = OrderFormPdfSettings.FileOf(_storage, _data.SaleNumber);
                byte[] _content = File.Exists(_path) ? await File.ReadAllBytesAsync(_path, _ct) : await SaveAsync(_data, _ct);
                return (new OrderFormPdfFile { Content = _content, FileName = Path.GetFileName(_path) }, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        private async Task<byte[]> SaveAsync(OrderFormPdfData _data, CancellationToken _ct)
        {
            OrderFormPdfFonts.EnsureRegistered(_fonts);
            byte[] _content = OrderFormPdfDocument.Render(_data, _settings, _assets);
            string _path = OrderFormPdfSettings.FileOf(_storage, _data.SaleNumber);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllBytesAsync(_path, _content, _ct);
            return _content;
        }

        /// <summary>The saved order as a Word file ({SaleNumber}.docx), made now from the database (not stored).</summary>
        public async Task<(OrderFormPdfFile file, IFunctionReturn functionReturn)> GetWordAsync(Guid _saleId, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:OrderFormPdf:GetWord";
            try
            {
                OrderFormPdfData _data = await ReadAsync(_saleId, _user.Id, _ct);
                if (_data == null) return (null, ApplicationCommonFunctions.NotFound("Sale not found, or it was not saved with the order form.", _methodName));
                byte[] _content = OrderFormWordDocument.Render(_data, _settings, _assets);
                return (new OrderFormPdfFile { Content = _content, FileName = $"{_data.SaleNumber}.docx" }, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        /// <summary>
        /// The order form as filled in on the page, not saved: a PDF (_format "pdf") or Word file ("word") named
        /// OrderForm_{Company}_{order date}. The POC is the user's manager (or the user, for a manager), as on a saved order.
        /// </summary>
        public async Task<(OrderFormPdfFile file, IFunctionReturn functionReturn)> PreviewAsync(HttpOrderFormPreviewParams _params, string _format, SalesUserContext _user, CancellationToken _ct = default)
        {
            string _methodName = "F:OrderFormPdf:Preview";
            try
            {
                string _kind = (_format ?? "pdf").Trim().ToLowerInvariant();
                if (_kind != "pdf" && _kind != "word") return (null, ApplicationCommonFunctions.BadRequest("Format must be pdf or word.", _methodName));
                (OrderFormPdfData _data, List<string> _errors) = CleanPreview(_params);
                if (_errors.Count > 0) return (null, OrderFormFunctions.Invalid(_errors, _methodName));

                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                    " SELECT LTRIM(RTRIM(CONCAT(poc.FirstName, ' ', poc.LastName))) AS PocName, poc.Email AS PocEmail " +
                    " FROM dbo.SalesUsers u JOIN dbo.SalesUsers poc ON poc.Id = ISNULL(u.ManagerId, u.Id) WHERE u.Id = @u ",
                    new[] { P("@u", _user.Id) }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                DataRow _poc = _dataTable.FirstRow();
                _data.PocName = _poc?.GetText("PocName");
                _data.PocEmail = _poc?.GetText("PocEmail");

                string _name = $"OrderForm_{FileSafe(_data.CustomerName)}_{_data.OrderDate:yyyy-MM-dd}";
                if (_kind == "word")
                    return (new OrderFormPdfFile { Content = OrderFormWordDocument.Render(_data, _settings, _assets), FileName = _name + ".docx" }, new FunctionReturn { MethodName = _methodName });
                OrderFormPdfFonts.EnsureRegistered(_fonts);
                return (new OrderFormPdfFile { Content = OrderFormPdfDocument.Render(_data, _settings, _assets), FileName = _name + ".pdf" }, new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        /// <summary>Cleans the page's values (tags removed, lengths and ranges checked) into what the documents print.</summary>
        private static (OrderFormPdfData data, List<string> errors) CleanPreview(HttpOrderFormPreviewParams _p)
        {
            var _errors = new List<string>();
            if (_p == null) return (null, new List<string> { "Fill in the order form first." });
            string Text(string _value, int _max) { string _c = Clean(_value); return _c != null && _c.Length > _max ? _c[.._max] : _c; }
            bool Amount(decimal _value) => _value >= 0 && _value <= 1_000_000_000_000m;

            string _customer = Text(_p.CustomerName, 200);
            if (string.IsNullOrEmpty(_customer)) _errors.Add("Choose a company first.");
            if (_p.StartDate == null || _p.EndDate == null || _p.EndDate < _p.StartDate) _errors.Add("Enter the start date and the paid months.");
            if (_p.Quantity < 1 || _p.Quantity > 100000) _errors.Add("Seats must be a whole number from 1 to 100000.");
            if (_p.TermMonths < 1 || _p.TermMonths > 240 || _p.FreeMonths < 0 || _p.FreeMonths > 120) _errors.Add("Choose the paid months.");
            if (_p.GstPercent < 0 || _p.GstPercent > 28) _errors.Add("GST must be from 0 to 28%.");
            if (new[] { _p.UnitPrice, _p.ListAmount, _p.DiscountAmount, _p.SaleAmount, _p.TaxAmount, _p.TotalAmount }.Any(a => !Amount(a))) _errors.Add("The amounts are not valid.");
            if (_p.IncludedItemQuantity is int _iq && (_iq < 1 || _iq > 1_000_000)) _errors.Add("Team Link Extend seats must be from 1 to 1000000.");
            OrderContact _primary = OrderFormFunctions.CleanContact(_p.PrimaryContact, "primary contact", false, _errors);
            OrderWording _wording = OrderFormFunctions.CleanWording(_p.Wording, _errors) ?? new OrderWording();
            if (_errors.Count > 0) return (null, _errors);

            DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));
            return (new OrderFormPdfData
            {
                SaleNumber = "Draft",
                OrderDate = _p.OrderDate ?? _today,
                StartDate = _p.StartDate!.Value,
                EndDate = _p.EndDate!.Value,
                TermMonths = _p.TermMonths,
                FreeMonths = _p.FreeMonths,
                CustomerName = _customer,
                ProductCode = Text(_p.ProductCode, 50) is string _code && System.Text.RegularExpressions.Regex.IsMatch(_code, "^[A-Za-z0-9_-]+$") ? _code : null,
                PrimaryContact = _primary,
                BillingFrequency = Text(_p.BillingFrequency, 100),
                BillingMethod = Text(_p.BillingMethod, 100),
                PaymentMethod = Text(_p.PaymentMethod, 100),
                PaymentTerm = Text(_p.PaymentTerm, 100),
                Currency = Text(_p.Currency, 20),
                Quantity = _p.Quantity,
                UnitPrice = _p.UnitPrice,
                ListAmount = _p.ListAmount,
                DiscountAmount = _p.DiscountAmount,
                SaleAmount = _p.SaleAmount,
                GstPercent = _p.GstPercent,
                TaxAmount = _p.TaxAmount,
                TotalAmount = _p.TotalAmount,
                IncludedItemQuantity = _p.IncludedItemQuantity,
                Wording = _wording
            }, _errors);
        }

        /// <summary>"Glacier Solutions Pvt Ltd" -> "Glacier_Solutions_Pvt_Ltd" (letters, digits, - and _ only).</summary>
        private static string FileSafe(string _name)
        {
            string _safe = System.Text.RegularExpressions.Regex.Replace(_name ?? string.Empty, "[^A-Za-z0-9-]+", "_").Trim('_');
            return _safe.Length == 0 ? "Company" : _safe.Length > 60 ? _safe[..60] : _safe;
        }

        /// <summary>The order as printed. _userId limits it to the seller or their manager (null = no limit, used right after saving).</summary>
        private async Task<OrderFormPdfData> ReadAsync(Guid _saleId, long? _userId, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT s.SaleNumber, s.CreatedAtUtc, s.StartDate, s.RenewalDate, s.DurationUnit, s.DurationCount, s.FreeMonths, ");
            _sqlQuery.Append("        s.Quantity, s.UnitPrice, s.ListAmount, s.DiscountAmount, s.SaleAmount, s.GstPercent, s.TaxAmount, s.TotalAmount, ");
            _sqlQuery.Append("        s.OrderWording, s.IncludedItemQuantity, ");
            _sqlQuery.Append("        s.PrimaryContactName, s.PrimaryContactDesignation, s.PrimaryContactEmail, s.PrimaryContactPhone, ");
            _sqlQuery.Append("        c.Name AS CustomerName, p.Code AS ProductCode, ");
            _sqlQuery.Append("        bf.Name AS BillingFrequency, bm.Name AS BillingMethod, pm.Name AS PaymentMethod, pt.Name AS PaymentTerm, cy.Name AS Currency, ");
            // POC: the seller's manager; a manager's own order shows the manager.
            _sqlQuery.Append("        LTRIM(RTRIM(CONCAT(poc.FirstName, ' ', poc.LastName))) AS PocName, poc.Email AS PocEmail ");
            _sqlQuery.Append(" FROM dbo.Sales s ");
            _sqlQuery.Append(" JOIN dbo.Customers c ON c.Id = s.CustomerId ");
            _sqlQuery.Append(" JOIN dbo.Products p ON p.Id = s.ProductId ");
            _sqlQuery.Append(" JOIN dbo.SalesUsers poc ON poc.Id = ISNULL(s.ManagerId, s.SalesUserId) ");
            _sqlQuery.Append(" LEFT JOIN dbo.MasterValues bf ON bf.Id = s.BillingFrequencyId ");
            _sqlQuery.Append(" LEFT JOIN dbo.MasterValues bm ON bm.Id = s.BillingMethodId ");
            _sqlQuery.Append(" LEFT JOIN dbo.MasterValues pm ON pm.Id = s.PaymentMethodId ");
            _sqlQuery.Append(" LEFT JOIN dbo.MasterValues pt ON pt.Id = s.PaymentTermId ");
            _sqlQuery.Append(" LEFT JOIN dbo.MasterValues cy ON cy.Id = s.CurrencyId ");
            _sqlQuery.Append(" WHERE s.PublicId = @id AND s.OrderWording IS NOT NULL ");
            if (_userId.HasValue) _sqlQuery.Append(" AND (s.SalesUserId = @u OR s.ManagerId = @u) ");

            var _parameters = _userId.HasValue ? new[] { P("@id", _saleId), P("@u", _userId.Value) } : new[] { P("@id", _saleId) };
            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), _parameters, "F:OrderFormPdf:Read", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            if (_row == null) return null;

            OrderWording _wording;
            try { _wording = JsonSerializer.Deserialize<OrderWording>(_row.GetText("OrderWording"), JsonOptions) ?? new OrderWording(); }
            catch (JsonException) { _wording = new OrderWording(); }

            string _unit = _row.GetText("DurationUnit");
            int _count = _row.GetInt("DurationCount");
            int _free = _row.GetInt("FreeMonths");
            int _paidMonths = _unit == "Year" ? 12 * _count : _unit == "Quarter" ? 3 * _count : _count;
            // The order date as a date in India.
            DateTime _createdUtc = DateTime.SpecifyKind(_row.GetUtc("CreatedAtUtc"), DateTimeKind.Utc);

            return new OrderFormPdfData
            {
                SaleNumber = _row.GetText("SaleNumber"),
                OrderDate = DateOnly.FromDateTime(_createdUtc.AddMinutes(330)),
                StartDate = _row.GetDate("StartDate"),
                EndDate = _row.GetDate("RenewalDate").AddDays(-1),
                TermMonths = _paidMonths + _free,
                FreeMonths = _free,
                CustomerName = _row.GetText("CustomerName"),
                ProductCode = _row.GetText("ProductCode"),
                PocName = _row.GetText("PocName"),
                PocEmail = _row.GetText("PocEmail"),
                PrimaryContact = new OrderContact
                {
                    Name = _row.GetText("PrimaryContactName"),
                    Designation = _row.GetText("PrimaryContactDesignation"),
                    Email = _row.GetText("PrimaryContactEmail"),
                    Phone = _row.GetText("PrimaryContactPhone")
                },
                BillingFrequency = _row.GetText("BillingFrequency"),
                BillingMethod = _row.GetText("BillingMethod"),
                PaymentMethod = _row.GetText("PaymentMethod"),
                PaymentTerm = _row.GetText("PaymentTerm"),
                Currency = _row.GetText("Currency"),
                Quantity = _row.GetInt("Quantity"),
                UnitPrice = _row.GetDecimal("UnitPrice"),
                ListAmount = _row.GetDecimal("ListAmount"),
                DiscountAmount = _row.GetDecimal("DiscountAmount"),
                SaleAmount = _row.GetDecimal("SaleAmount"),
                GstPercent = _row.GetDecimal("GstPercent"),
                TaxAmount = _row.GetDecimal("TaxAmount"),
                TotalAmount = _row.GetDecimal("TotalAmount"),
                IncludedItemQuantity = _row.GetNullableInt("IncludedItemQuantity"),
                Wording = _wording
            };
        }
    }
}
