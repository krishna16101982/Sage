using System;
using System.IO;

namespace SageUserApi.Models
{
    /// <summary>
    /// appsettings "OrderFormPdf": where order form PDFs are saved and the fixed header of the PDF.
    /// Relative paths are from the API's content root. The Admin API reads the same StoragePath.
    /// </summary>
    public class OrderFormPdfSettings
    {
        /// <summary>One folder per order number: {StoragePath}/{SaleNumber}/{SaleNumber}.pdf.</summary>
        public string StoragePath { get; set; } = "../../../storage/order-forms";
        /// <summary>i2k2-logo.png and product-{ProductCode}.png (e.g. product-LinkedIn.png, the LinkedIn partner logo).</summary>
        public string AssetsPath { get; set; } = "Assets/OrderForm";
        /// <summary>Optional Roboto-Regular.ttf / Roboto-Bold.ttf; otherwise the system's Segoe UI (Windows) or DejaVu Sans (Linux).</summary>
        public string FontsPath { get; set; } = "Assets/Fonts";
        public string CompanyName { get; set; } = "i2k2 Networks Pvt. Ltd.";
        public string CompanyAddressLine1 { get; set; } = "Spring Meadows Business Park, A-61/B4,";
        public string CompanyAddressLine2 { get; set; } = "Rasoolpur, Sector 63, Noida, UP 201309";
        /// <summary>Label over the seller's manager (or the manager who made the order).</summary>
        public string PocLabel { get; set; } = "Your i2k2 Networks POC";
        /// <summary>"{SignatureName} Signature" box on page 2.</summary>
        public string SignatureName { get; set; } = "i2k2 Networks";

        /// <summary>An absolute path for a configured one (relative = from the content root).</summary>
        public static string Resolve(string _contentRoot, string _path)
            => Path.IsPathRooted(_path) ? _path : Path.GetFullPath(Path.Combine(_contentRoot, _path));

        /// <summary>{root}/{SaleNumber}/{SaleNumber}.pdf (characters not allowed in file names become '_').</summary>
        public static string FileOf(string _root, string _saleNumber)
        {
            string _safe = string.Concat(_saleNumber.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_root, _safe, _safe + ".pdf");
        }
    }

    /// <summary>A PDF ready to send.</summary>
    public class OrderFormPdfFile
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
    }

    /// <summary>Everything printed on the order form PDF, read from the saved order.</summary>
    public class OrderFormPdfData
    {
        public string SaleNumber { get; set; }
        public DateOnly OrderDate { get; set; }
        public DateOnly StartDate { get; set; }
        /// <summary>The last day: renewal date less one day.</summary>
        public DateOnly EndDate { get; set; }
        public int TermMonths { get; set; }
        public int FreeMonths { get; set; }
        public string CustomerName { get; set; }
        public string ProductCode { get; set; }
        public string PocName { get; set; }
        public string PocEmail { get; set; }
        public OrderContact PrimaryContact { get; set; }
        public string BillingFrequency { get; set; }
        public string BillingMethod { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentTerm { get; set; }
        public string Currency { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal ListAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal SaleAmount { get; set; }
        public decimal GstPercent { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public int? IncludedItemQuantity { get; set; }
        public OrderWording Wording { get; set; }
    }
}
