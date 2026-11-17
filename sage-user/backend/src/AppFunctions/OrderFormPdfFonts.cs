using System;
using System.IO;
using PdfSharp.Fonts;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// Fonts for the order form PDF. Every family the document asks for maps to one regular and one bold face that has
    /// the rupee sign: Roboto from the configured fonts folder when present, else Segoe UI (Windows) or DejaVu Sans
    /// (Linux). PDFsharp allows one resolver per process, so it is set once.
    /// </summary>
    public class OrderFormPdfFonts : IFontResolver
    {
        public const string Family = "OrderForm";
        private const string RegularFace = "OrderForm#Regular";
        private const string BoldFace = "OrderForm#Bold";
        private static readonly object Gate = new object();
        private readonly string _regular;
        private readonly string _bold;

        private OrderFormPdfFonts(string __regular, string __bold)
        {
            _regular = __regular;
            _bold = __bold;
        }

        /// <summary>Sets the resolver the first time a PDF is made.</summary>
        public static void EnsureRegistered(string _fontsFolder)
        {
            if (GlobalFontSettings.FontResolver is OrderFormPdfFonts) return;
            lock (Gate)
            {
                if (GlobalFontSettings.FontResolver is OrderFormPdfFonts) return;
                (string _regular, string _bold) = Find(_fontsFolder);
                GlobalFontSettings.FontResolver = new OrderFormPdfFonts(_regular, _bold);
            }
        }

        private static (string regular, string bold) Find(string _fontsFolder)
        {
            string _windows = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var _candidates = new (string regular, string bold)[]
            {
                (Path.Combine(_fontsFolder ?? string.Empty, "Roboto-Regular.ttf"), Path.Combine(_fontsFolder ?? string.Empty, "Roboto-Bold.ttf")),
                (Path.Combine(_windows, "segoeui.ttf"), Path.Combine(_windows, "segoeuib.ttf")),
                ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
                (Path.Combine(_windows, "arial.ttf"), Path.Combine(_windows, "arialbd.ttf")),
            };
            foreach (var _pair in _candidates)
                if (File.Exists(_pair.regular) && File.Exists(_pair.bold)) return _pair;
            throw new InvalidOperationException("No font found for the order form PDF: put Roboto-Regular.ttf and Roboto-Bold.ttf in the OrderFormPdf:FontsPath folder.");
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic)
            => new FontResolverInfo(bold ? BoldFace : RegularFace);

        public byte[] GetFont(string faceName)
            => File.ReadAllBytes(faceName == BoldFace ? _bold : _regular);
    }
}
