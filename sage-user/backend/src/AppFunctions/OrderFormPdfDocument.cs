using System;
using System.Globalization;
using System.IO;
using System.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SageUserApi.Models;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// Lays out the order form PDF like the i2k2 / LinkedIn sample: fixed header (logos, company, POC), customer and
    /// contact, order information, line items (included item at 0.00 and the product), totals, tax information,
    /// authorized signatures and footer. Text comes from the order's saved wording sections.
    /// </summary>
    public static class OrderFormPdfDocument
    {
        private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
        private static readonly Color Ink = new Color(33, 37, 41);
        private static readonly Color Muted = new Color(90, 96, 104);
        private static readonly Color Heading = new Color(60, 64, 72);
        private static readonly Color Navy = new Color(0, 45, 130);
        private static readonly Color Link = new Color(0, 0, 238);
        private static readonly Color Shade = new Color(242, 242, 242);
        private static readonly Color Band = new Color(64, 64, 64);
        private static readonly Color Line = new Color(200, 200, 200);
        private const double ContentWidthCm = 17.8;

        public static byte[] Render(OrderFormPdfData _d, OrderFormPdfSettings _settings, string _assetsFolder)
        {
            var _doc = new Document();
            _doc.Info.Title = $"Order form {_d.SaleNumber}";
            _doc.Info.Author = _settings.CompanyName;
            Style _normal = _doc.Styles[StyleNames.Normal]!;
            _normal.Font.Name = OrderFormPdfFonts.Family;
            _normal.Font.Size = 9.5;
            _normal.Font.Color = Ink;

            Section _section = _doc.AddSection();
            _section.PageSetup = _doc.DefaultPageSetup.Clone();
            _section.PageSetup.PageFormat = PageFormat.A4;
            _section.PageSetup.LeftMargin = Unit.FromCentimeter(1.6);
            _section.PageSetup.RightMargin = Unit.FromCentimeter(1.6);
            _section.PageSetup.TopMargin = Unit.FromCentimeter(1.3);
            _section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);

            AddHeader(_section, _d, _settings, _assetsFolder);
            AddCustomer(_section, _d);
            AddOrderInformation(_section, _d);
            AddItems(_section, _d);
            AddTotals(_section, _d);
            AddSignatures(_section, _d, _settings);

            var _renderer = new PdfDocumentRenderer { Document = _doc };
            _renderer.RenderDocument();
            using var _stream = new MemoryStream();
            _renderer.PdfDocument.Save(_stream, false);
            return _stream.ToArray();
        }

        // ---------------- sections ----------------

        private static void AddHeader(Section _s, OrderFormPdfData _d, OrderFormPdfSettings _settings, string _assets)
        {
            Table _t = _s.AddTable();
            _t.AddColumn(Unit.FromCentimeter(3.6));
            _t.AddColumn(Unit.FromCentimeter(8.2));
            _t.AddColumn(Unit.FromCentimeter(6.0));
            Row _r = _t.AddRow();
            _r.VerticalAlignment = VerticalAlignment.Center;
            string _logo = Path.Combine(_assets, "i2k2-logo.png");
            if (File.Exists(_logo)) _r.Cells[0].AddParagraph().AddImage(_logo).Width = Unit.FromCentimeter(3.2);

            Paragraph _company = _r.Cells[1].AddParagraph();
            _company.Format.Font.Size = 8;
            _company.Format.Font.Color = Muted;
            _company.AddFormattedText(_settings.CompanyName, TextFormat.Bold).Color = Ink;
            _company.AddLineBreak();
            _company.AddText(_settings.CompanyAddressLine1);
            _company.AddLineBreak();
            _company.AddText(_settings.CompanyAddressLine2);

            Paragraph _poc = _r.Cells[2].AddParagraph();
            _poc.Format.Alignment = ParagraphAlignment.Right;
            _poc.Format.Font.Size = 8.5;
            _poc.AddFormattedText(_settings.PocLabel, TextFormat.Bold);
            if (!string.IsNullOrWhiteSpace(_d.PocName)) { _poc.AddLineBreak(); _poc.AddText(_d.PocName); }
            if (!string.IsNullOrWhiteSpace(_d.PocEmail)) { _poc.AddLineBreak(); AddEmail(_poc, _d.PocEmail); }

            HRule(_s, 0.75, Line, 4, 6);

            string _productLogo = Path.Combine(_assets, $"product-{_d.ProductCode}.png");
            if (File.Exists(_productLogo))
            {
                Paragraph _p = _s.AddParagraph();
                _p.Format.Alignment = ParagraphAlignment.Center;
                _p.Format.SpaceAfter = Unit.FromPoint(4);
                _p.AddImage(_productLogo).Width = Unit.FromCentimeter(4.0);
            }
        }

        private static void AddCustomer(Section _s, OrderFormPdfData _d)
        {
            Paragraph _for = _s.AddParagraph($"Order form for: {_d.CustomerName}");
            _for.Format.Alignment = ParagraphAlignment.Center;
            _for.Format.Font.Size = 16;
            _for.Format.Font.Color = Heading;

            Paragraph _date = _s.AddParagraph($"Date: {Dmy(_d.OrderDate)}");
            _date.Format.Alignment = ParagraphAlignment.Center;
            _date.Format.Font.Size = 9;
            _date.Format.SpaceAfter = Unit.FromPoint(10);

            OrderWording _w = _d.Wording;
            if (Has(_w.SignReturnLine))
            {
                Paragraph _sign = _s.AddParagraph(_w.SignReturnLine);
                _sign.Format.Alignment = ParagraphAlignment.Center;
                _sign.Format.Font.Bold = true;
                _sign.Format.SpaceAfter = Unit.FromPoint(3);
            }
            if (Has(_w.SignReturnInstruction))
            {
                Paragraph _how = _s.AddParagraph(_w.SignReturnInstruction);
                _how.Format.Alignment = ParagraphAlignment.Center;
                _how.Format.Font.Size = 8.5;
                _how.Format.Font.Color = Muted;
            }
            HRule(_s, 0.5, Line, 6, 8, 1.0);

            OrderContact _c = _d.PrimaryContact;
            if (_c != null)
            {
                Paragraph _p = _s.AddParagraph();
                _p.Format.LeftIndent = Unit.FromCentimeter(0.2);
                _p.AddFormattedText("Primary Contact:", TextFormat.Bold);
                foreach (string _line in new[] { _c.Name, _c.Designation }.Where(Has)) { _p.AddLineBreak(); _p.AddText(_line); }
                if (Has(_c.Email)) { _p.AddLineBreak(); AddEmail(_p, _c.Email); }
                if (Has(_c.Phone)) { _p.AddLineBreak(); _p.AddText($"Ph. No: {_c.Phone}"); }
            }
            HRule(_s, 1.25, Ink, 6, 10);
        }

        private static void AddOrderInformation(Section _s, OrderFormPdfData _d)
        {
            Paragraph _h = _s.AddParagraph("Order Information");
            _h.Format.Font.Size = 13;
            _h.Format.SpaceAfter = Unit.FromPoint(8);
            _h.Format.LeftIndent = Unit.FromCentimeter(0.2);

            Table _t = _s.AddTable();
            _t.TopPadding = Unit.FromPoint(3);
            _t.BottomPadding = Unit.FromPoint(6);
            _t.LeftPadding = Unit.FromCentimeter(0.3);
            foreach (double _w in new[] { 6.0, 6.3, 5.5 }) _t.AddColumn(Unit.FromCentimeter(_w));
            AddInfoRow(_t, ("Start Date:", LongDate(_d.StartDate, " - ")), ("End Date:", LongDate(_d.EndDate, " - ")), ("Term:", $"{_d.TermMonths} months"));
            AddInfoRow(_t, ("Billing Frequency:", _d.BillingFrequency), ("Billing Method:", _d.BillingMethod), ("Payment Method:", _d.PaymentMethod));
            AddInfoRow(_t, ("Payment term:", _d.PaymentTerm), ("Currency:", _d.Currency), (null, null));

            OrderWording _w2 = _d.Wording;
            if (Has(_w2.OrderNotes) || (_d.FreeMonths > 0 && Has(_w2.FreeMonthNote)))
            {
                Paragraph _notes = _s.AddParagraph();
                _notes.Format.SpaceBefore = Unit.FromPoint(6);
                _notes.Format.SpaceAfter = Unit.FromPoint(10);
                _notes.Format.LeftIndent = Unit.FromCentimeter(0.2);
                _notes.Format.Font.Size = 9;
                _notes.AddFormattedText("Order Notes: ", TextFormat.Bold);
                if (Has(_w2.OrderNotes)) _notes.AddText(_w2.OrderNotes.Trim() + " ");
                if (_d.FreeMonths > 0 && Has(_w2.FreeMonthNote)) _notes.AddFormattedText(System.Text.RegularExpressions.Regex.Replace(_w2.FreeMonthNote.Trim(), @"\b1 free months\b", "1 free month"), TextFormat.Bold);
            }
        }

        private static void AddItems(Section _s, OrderFormPdfData _d)
        {
            Table _t = _s.AddTable();
            _t.Borders.Color = Ink;
            _t.Borders.Width = 0.75;
            _t.TopPadding = Unit.FromPoint(6);
            _t.BottomPadding = Unit.FromPoint(10);
            _t.LeftPadding = Unit.FromPoint(5);
            _t.RightPadding = Unit.FromPoint(5);
            Column _desc = _t.AddColumn(Unit.FromCentimeter(9.8));
            _t.AddColumn(Unit.FromCentimeter(2.7));
            _t.AddColumn(Unit.FromCentimeter(2.6)).Format.Alignment = ParagraphAlignment.Center;
            _t.AddColumn(Unit.FromCentimeter(2.7));

            Row _head = _t.AddRow();
            _head.HeadingFormat = true;
            _head.Shading.Color = Navy;
            _head.Format.Font.Color = Colors.White;
            _head.Format.Font.Size = 10.5;
            _head.TopPadding = Unit.FromPoint(9);
            _head.BottomPadding = Unit.FromPoint(9);
            _head.Cells[0].AddParagraph("Description");
            _head.Cells[1].AddParagraph("Unit Price");
            _head.Cells[2].AddParagraph("Quantity");
            _head.Cells[3].AddParagraph("Total");

            OrderWording _w = _d.Wording;
            if (Has(_w.ExtraItemTitle))
            {
                Row _r = _t.AddRow();
                _r.Shading.Color = Shade;
                ItemText(_r.Cells[0], _w.ExtraItemTitle, _w.ExtraItemDescription, null);
                _r.Cells[1].AddParagraph($"₹ {Money(0m)}");
                _r.Cells[2].AddParagraph(_d.IncludedItemQuantity?.ToString(India) ?? string.Empty);
                _r.Cells[3].AddParagraph($"₹ {Money(0m)}");
            }

            Row _p = _t.AddRow();
            ItemText(_p.Cells[0], _w.ProductTitle, _w.ProductDescription, _w.PeriodLine);
            _p.Cells[1].AddParagraph($"₹ {Money(_d.UnitPrice)}");
            _p.Cells[2].AddParagraph(_d.Quantity.ToString(India));
            _p.Cells[3].AddParagraph($"₹ {Money(_d.ListAmount)}");
        }

        private static void AddTotals(Section _s, OrderFormPdfData _d)
        {
            _s.AddParagraph().Format.SpaceAfter = Unit.FromPoint(14);
            Table _t = _s.AddTable();
            _t.Rows.Alignment = RowAlignment.Right;
            _t.Borders.Color = Ink;
            _t.Borders.Width = 0.75;
            _t.TopPadding = Unit.FromPoint(6);
            _t.BottomPadding = Unit.FromPoint(6);
            _t.LeftPadding = Unit.FromPoint(5);
            _t.RightPadding = Unit.FromPoint(5);
            _t.AddColumn(Unit.FromCentimeter(6.2));
            _t.AddColumn(Unit.FromCentimeter(4.2)).Format.Alignment = ParagraphAlignment.Right;
            _t.Format.Font.Size = 10;

            TotalRow(_t, "Subtotal:", Fixed(_d.ListAmount), false, true);
            if (_d.DiscountAmount > 0 && _d.ListAmount > 0)
            {
                decimal _percent = Math.Round(_d.DiscountAmount * 100m / _d.ListAmount, 2);
                TotalRow(_t, $"After {_percent.ToString("0.##", CultureInfo.InvariantCulture)}% Discount", Fixed(_d.SaleAmount), false, false);
            }
            TotalRow(_t, $"Estimated Tax ({_d.GstPercent.ToString("0.##", CultureInfo.InvariantCulture)}% GST):", Fixed(_d.TaxAmount), false, false);
            TotalRow(_t, $"Estimated Order Total ({_d.Currency ?? "INR"}):", Fixed(_d.TotalAmount), true, true);
            // The totals stay together on one page.
            _t.Rows[0].KeepWith = _t.Rows.Count - 1;

            if (Has(_d.Wording.TaxInformation))
            {
                Paragraph _tax = _s.AddParagraph(_d.Wording.TaxInformation.Trim());
                _tax.Format.Font.Size = 7.5;
                _tax.Format.Font.Color = Muted;
                _tax.Format.SpaceBefore = Unit.FromPoint(10);
                _tax.Format.SpaceAfter = Unit.FromPoint(10);
            }
        }

        private static void AddSignatures(Section _s, OrderFormPdfData _d, OrderFormPdfSettings _settings)
        {
            string[] _clauses = Lines(_d.Wording.SignatureClauses);
            Table _band = _s.AddTable();
            _band.AddColumn(Unit.FromCentimeter(ContentWidthCm));
            _band.Borders.Color = Line;
            _band.Borders.Width = 0.5;
            _band.LeftPadding = Unit.FromPoint(10);
            _band.RightPadding = Unit.FromPoint(10);
            Row _title = _band.AddRow();
            _title.Shading.Color = Band;
            _title.TopPadding = Unit.FromPoint(8);
            _title.BottomPadding = Unit.FromPoint(8);
            Paragraph _tp = _title.Cells[0].AddParagraph("Authorized Signatures");
            _tp.Format.Font.Color = Colors.White;
            _tp.Format.Font.Size = 11.5;
            if (_clauses.Length > 0)
            {
                Row _body = _band.AddRow();
                _body.TopPadding = Unit.FromPoint(6);
                _body.BottomPadding = Unit.FromPoint(4);
                for (int i = 0; i < _clauses.Length; i++)
                {
                    Paragraph _p = _body.Cells[0].AddParagraph();
                    _p.Format.Font.Size = 8.5;
                    _p.Format.LeftIndent = Unit.FromCentimeter(0.6);
                    _p.Format.FirstLineIndent = Unit.FromCentimeter(-0.6);
                    _p.Format.SpaceAfter = Unit.FromPoint(4);
                    _p.Format.AddTabStop(Unit.FromCentimeter(0.6));
                    _p.AddText($"{i + 1}.");
                    _p.AddTab();
                    // "Order Cancellation: ..." - a short lead-in before the first colon is bold.
                    string _clause = _clauses[i];
                    int _colon = _clause.IndexOf(':');
                    if (_colon > 0 && _colon <= 30 && !_clause[.._colon].Contains("http", StringComparison.OrdinalIgnoreCase))
                    {
                        _p.AddFormattedText(_clause[..(_colon + 1)], TextFormat.Bold);
                        _p.AddText(_clause[(_colon + 1)..]);
                    }
                    else _p.AddText(_clause);
                }
            }

            _s.AddParagraph().Format.SpaceAfter = Unit.FromPoint(10);
            Table _boxes = _s.AddTable();
            _boxes.AddColumn(Unit.FromCentimeter(8.1));
            _boxes.AddColumn(Unit.FromCentimeter(1.6));
            _boxes.AddColumn(Unit.FromCentimeter(8.1));
            _boxes.KeepTogether = true;
            Row _row = _boxes.AddRow();
            SignatureBox(_row.Cells[0], "Customer Signature", "[Authorized Signatory Name]", "[Designation]", "[Date]");
            SignatureBox(_row.Cells[2], $"{_settings.SignatureName} Signature", "[Authorized Signatory]", _settings.CompanyName, $"[{LongDate(_d.OrderDate, " ")}]");

            string[] _footer = Lines(_d.Wording.Footer);
            if (_footer.Length > 0)
            {
                HRule(_s, 0.5, Line, 10, 6);
                foreach (string _line in _footer)
                {
                    Paragraph _p = _s.AddParagraph(_line);
                    _p.Format.Alignment = ParagraphAlignment.Center;
                    _p.Format.Font.Size = 8.5;
                    _p.Format.Font.Color = Muted;
                    _p.Format.SpaceAfter = Unit.FromPoint(3);
                }
            }
        }

        // ---------------- pieces ----------------

        private static void AddInfoRow(Table _t, params (string label, string value)[] _cells)
        {
            Row _r = _t.AddRow();
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].label == null) continue;
                Paragraph _p = _r.Cells[i].AddParagraph();
                _p.AddFormattedText(_cells[i].label + " ", TextFormat.Bold);
                _p.AddText(_cells[i].value ?? "-");
            }
        }

        private static void ItemText(Cell _cell, string _title, string _description, string _period)
        {
            if (Has(_title))
            {
                Paragraph _t = _cell.AddParagraph(_title.Trim());
                _t.Format.Font.Size = 10.5;
                _t.Format.SpaceAfter = Unit.FromPoint(2);
            }
            if (Has(_description))
            {
                Paragraph _d = _cell.AddParagraph(_description.Trim());
                _d.Format.Font.Size = 9;
            }
            if (Has(_period))
            {
                Paragraph _p = _cell.AddParagraph(_period.Trim());
                _p.Format.Font.Size = 9;
                _p.Format.Font.Bold = true;
                _p.Format.SpaceBefore = Unit.FromPoint(4);
            }
        }

        private static void TotalRow(Table _t, string _label, string _value, bool _bold, bool _boldValue)
        {
            Row _r = _t.AddRow();
            _r.Format.Font.Bold = _bold;
            _r.Cells[0].AddParagraph(_label);
            Paragraph _v = _r.Cells[1].AddParagraph(_value);
            _v.Format.Font.Bold = _bold || _boldValue;
        }

        private static void SignatureBox(Cell _cell, string _title, params string[] _lines)
        {
            _cell.Borders.Color = Line;
            _cell.Borders.Width = 0.5;
            _cell.Format.LeftIndent = Unit.FromPoint(10);
            _cell.Format.RightIndent = Unit.FromPoint(10);
            Paragraph _t = _cell.AddParagraph(_title);
            _t.Format.Font.Bold = true;
            _t.Format.SpaceBefore = Unit.FromPoint(10);
            _t.Format.SpaceAfter = Unit.FromPoint(24);
            Paragraph _line = _cell.AddParagraph(new string('_', 38));
            _line.Format.SpaceAfter = Unit.FromPoint(6);
            for (int i = 0; i < _lines.Length; i++)
            {
                Paragraph _p = _cell.AddParagraph(_lines[i] ?? string.Empty);
                _p.Format.SpaceAfter = Unit.FromPoint(i == _lines.Length - 1 ? 10 : 6);
            }
        }

        /// <summary>A horizontal line: an empty paragraph with a bottom border.</summary>
        private static void HRule(Section _s, double _width, Color _color, double _before, double _after, double _indentCm = 0)
        {
            Paragraph _p = _s.AddParagraph();
            _p.Format.Font.Size = 1;
            _p.Format.SpaceBefore = Unit.FromPoint(_before);
            _p.Format.SpaceAfter = Unit.FromPoint(_after);
            _p.Format.LeftIndent = Unit.FromCentimeter(_indentCm);
            _p.Format.RightIndent = Unit.FromCentimeter(_indentCm);
            _p.Format.Borders.Bottom.Width = _width;
            _p.Format.Borders.Bottom.Color = _color;
        }

        private static void AddEmail(Paragraph _p, string _email)
        {
            Hyperlink _h = _p.AddHyperlink("mailto:" + _email.Trim(), HyperlinkType.Url);
            FormattedText _text = _h.AddFormattedText(_email.Trim());
            _text.Color = Link;
            _text.Underline = Underline.Single;
        }

        // ---------------- formats ----------------

        private static bool Has(string _value) => !string.IsNullOrWhiteSpace(_value);

        private static string[] Lines(string _text)
            => (_text ?? string.Empty).Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

        /// <summary>16-11-2026</summary>
        public static string Dmy(DateOnly _date) => _date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

        /// <summary>"16th - November - 2026" (separator " - ") or "16th November 2026" (separator " ").</summary>
        public static string LongDate(DateOnly _date, string _separator)
        {
            int _d = _date.Day;
            string _suffix = (_d % 10 == 1 && _d != 11) ? "st" : (_d % 10 == 2 && _d != 12) ? "nd" : (_d % 10 == 3 && _d != 13) ? "rd" : "th";
            return $"{_d}{_suffix}{_separator}{_date.ToString("MMMM", CultureInfo.InvariantCulture)}{_separator}{_date.Year}";
        }

        /// <summary>Indian grouping: 1,11,578 for whole rupees, 1,11,578.50 with paise, 0.00 for zero.</summary>
        public static string Money(decimal _amount)
            => _amount == 0m ? "0.00" : _amount == decimal.Truncate(_amount) ? _amount.ToString("N0", India) : _amount.ToString("N2", India);

        /// <summary>Always two decimals: 5,57,890.00</summary>
        public static string Fixed(decimal _amount) => _amount.ToString("N2", India);
    }
}
