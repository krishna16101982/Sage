using System;
using System.Data;
using System.IO;
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
