using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SageAdminApi.AppValidations;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>A saved order form PDF to send.</summary>
    public class OrderFormPdfFile
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
    }

    public interface IOrderFormPdfStore
    {
        /// <summary>The order form PDF the User Panel saved for this sale ({StoragePath}/{SaleNumber}/{SaleNumber}.pdf).</summary>
        Task<(OrderFormPdfFile file, IFunctionReturn functionReturn)> GetAsync(Guid _saleId, CancellationToken _ct = default);
    }

    /// <summary>
    /// Reads order form PDFs from the folder the User API saves them in (appsettings OrderFormPdf:StoragePath, same
    /// value in both APIs; relative = from the content root). The Admin Panel only opens them; it never makes them.
    /// </summary>
    public class OrderFormPdfStore : IOrderFormPdfStore
    {
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly string _storage;

        public OrderFormPdfStore(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IConfiguration __configuration, IWebHostEnvironment __env)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            string _path = __configuration["OrderFormPdf:StoragePath"] ?? "../../../storage/order-forms";
            _storage = Path.IsPathRooted(_path) ? _path : Path.GetFullPath(Path.Combine(__env.ContentRootPath, _path));
        }

        public async Task<(OrderFormPdfFile file, IFunctionReturn functionReturn)> GetAsync(Guid _saleId, CancellationToken _ct = default)
        {
            string _methodName = "F:OrderFormPdf:Get";
            try
            {
                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                    " SELECT SaleNumber, CASE WHEN OrderWording IS NULL THEN 0 ELSE 1 END AS HasOrderForm FROM dbo.Sales WHERE PublicId = @id ",
                    new[] { P("@id", _saleId) }, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);
                DataRow _row = _dataTable.FirstRow();
                if (_row == null) return (null, ApplicationCommonFunctions.NotFound("Sale not found", _methodName));
                if (_row.GetInt("HasOrderForm") == 0)
                    return (null, ApplicationCommonFunctions.NotFound("This sale was not saved with the order form, so it has no order form PDF.", _methodName));

                string _number = string.Concat(_row.GetText("SaleNumber").Split(Path.GetInvalidFileNameChars()));
                string _file = Path.Combine(_storage, _number, _number + ".pdf");
                if (!File.Exists(_file))
                    return (null, ApplicationCommonFunctions.NotFound("The order form PDF is not saved yet. Open the order once in the User Panel to make it.", _methodName));

                return (new OrderFormPdfFile { Content = await File.ReadAllBytesAsync(_file, _ct), FileName = _number + ".pdf" },
                        new FunctionReturn { MethodName = _methodName });
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
