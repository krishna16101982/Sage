using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SageUserApi.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// The order form as a Word file (.docx), the same layout and wording as the PDF (OrderFormPdfDocument): header with
    /// logo and POC, customer, order information, items, totals, tax note, authorized signatures and footer. The wording
    /// keeps its formatting (bold, italic, underline, colour, highlight); Word can highlight single words, so a partly
    /// highlighted line shows its highlight here. Made on request from the saved order, so the seller can edit it in Word.
    /// </summary>
    public static class OrderFormWordDocument
    {
        // Colours as in the PDF.
        private const string Ink = "212529";
        private const string Muted = "5A6068";
        private const string Heading = "3C4048";
        private const string Navy = "002D82";
        private const string Link = "0000EE";
        private const string Shade = "F2F2F2";
        private const string Band = "404040";
        private const string Line = "C8C8C8";
        private const string Font = "Arial";
        private const double ContentWidthCm = 17.8;

        private static int Twips(double _cm) => (int)Math.Round(_cm * 567);
        private static string HalfPoints(double _pt) => ((int)Math.Round(_pt * 2)).ToString(CultureInfo.InvariantCulture);

        public static byte[] Render(OrderFormPdfData _d, OrderFormPdfSettings _settings, string _assetsFolder)
        {
            using var _stream = new MemoryStream();
            using (WordprocessingDocument _doc = WordprocessingDocument.Create(_stream, WordprocessingDocumentType.Document))
            {
                MainDocumentPart _main = _doc.AddMainDocumentPart();
                _main.Document = new Document(new Body());
                AddStyles(_main);
                _doc.PackageProperties.Title = $"Order form {_d.SaleNumber}";
                _doc.PackageProperties.Creator = _settings.CompanyName;

                var _w = new Writer(_main);
                AddHeader(_w, _d, _settings, _assetsFolder);
                AddCustomer(_w, _d);
                AddOrderInformation(_w, _d);
                AddItems(_w, _d);
                AddTotals(_w, _d);
                AddSignatures(_w, _d, _settings);

                _main.Document.Body!.Append(new SectionProperties(
                    new PageSize { Width = 11906U, Height = 16838U },
                    new PageMargin { Top = Twips(1.3), Bottom = Twips(1.5), Left = (uint)Twips(1.6), Right = (uint)Twips(1.6), Header = 400U, Footer = 400U, Gutter = 0U }));
                _main.Document.Save();
            }
            return _stream.ToArray();
        }

        private static void AddStyles(MainDocumentPart _main)
        {
            StyleDefinitionsPart _styles = _main.AddNewPart<StyleDefinitionsPart>();
            _styles.Styles = new Styles(new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font, EastAsia = Font },
                    new Color { Val = Ink },
                    new FontSize { Val = HalfPoints(9.5) },
                    new FontSizeComplexScript { Val = HalfPoints(9.5) })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { Before = "0", After = "0", Line = "264", LineRule = LineSpacingRuleValues.Auto }))));
            _styles.Styles.Save();
        }

        // ---------------- sections (same order and look as the PDF) ----------------

        private static void AddHeader(Writer _w, OrderFormPdfData _d, OrderFormPdfSettings _settings, string _assets)
        {
            Table _t = _w.Table(new[] { 3.6, 8.2, 6.0 }, _borders: false);
            TableRow _r = _w.Row(_t);
            string _logo = Path.Combine(_assets, "i2k2-logo.png");
            Paragraph _logoP = _w.Cell(_r, 0, _vCenter: true);
            if (File.Exists(_logo)) _logoP.Append(_w.Image(_logo, 3.2));

            Paragraph _company = _w.Cell(_r, 1, _vCenter: true);
            _company.Append(Run(_settings.CompanyName, _size: 8, _bold: true, _color: Ink), Break(),
                Run(_settings.CompanyAddressLine1, _size: 8, _color: Muted), Break(), Run(_settings.CompanyAddressLine2, _size: 8, _color: Muted));

            Paragraph _poc = _w.Cell(_r, 2, _vCenter: true);
            Align(_poc, JustificationValues.Right);
            _poc.Append(Run(_settings.PocLabel, _size: 8.5, _bold: true));
            if (Has(_d.PocName)) _poc.Append(Break(), Run(_d.PocName, _size: 8.5));
            if (Has(_d.PocEmail)) _poc.Append(Break(), _w.Email(_d.PocEmail, 8.5));

            _w.HRule(0.75, Line, 4, 6);
            string _productLogo = Path.Combine(_assets, $"product-{_d.ProductCode}.png");
            if (File.Exists(_productLogo))
            {
                Paragraph _p = _w.Para(_after: 4);
                Align(_p, JustificationValues.Center);
                _p.Append(_w.Image(_productLogo, 4.0));
            }
        }

        private static void AddCustomer(Writer _w, OrderFormPdfData _d)
        {
            Paragraph _for = _w.Para();
            Align(_for, JustificationValues.Center);
            _for.Append(Run($"Order form for: {_d.CustomerName}", _size: 16, _color: Heading));
            Paragraph _date = _w.Para(_after: 10);
            Align(_date, JustificationValues.Center);
            _date.Append(Run($"Date: {OrderFormPdfDocument.Dmy(_d.OrderDate)}", _size: 9));

            OrderWording _wd = _d.Wording;
            if (Has(_wd.SignReturnLine))
            {
                Paragraph _sign = _w.Para(_after: 3);
                Align(_sign, JustificationValues.Center);
                Rich(_sign, _wd.SignReturnLine, _size: 9.5, _allBold: true);
            }
            if (Has(_wd.SignReturnInstruction))
            {
                Paragraph _how = _w.Para();
                Align(_how, JustificationValues.Center);
                Rich(_how, _wd.SignReturnInstruction, _size: 8.5, _color: Muted);
            }
            _w.HRule(0.5, Line, 6, 8, 1.0);

            OrderContact _c = _d.PrimaryContact;
            if (_c != null)
            {
                Paragraph _p = _w.Para(_indentCm: 0.2);
                _p.Append(Run("Primary Contact:", _bold: true));
                foreach (string _line in new[] { _c.Name, _c.Designation }.Where(Has)) _p.Append(Break(), Run(_line));
                if (Has(_c.Email)) _p.Append(Break(), _w.Email(_c.Email, 9.5));
                if (Has(_c.Phone)) _p.Append(Break(), Run($"Ph. No: {_c.Phone}"));
            }
            _w.HRule(1.25, Ink, 6, 10);
        }

        private static void AddOrderInformation(Writer _w, OrderFormPdfData _d)
        {
            Paragraph _h = _w.Para(_after: 8, _indentCm: 0.2);
            _h.Append(Run("Order Information", _size: 13));

            Table _t = _w.Table(new[] { 6.0, 6.3, 5.5 }, _borders: false, _padTopPt: 3, _padBottomPt: 6, _padLeftCm: 0.3);
            InfoRow(_w, _t, ("Start Date:", OrderFormPdfDocument.LongDate(_d.StartDate, " - ")), ("End Date:", OrderFormPdfDocument.LongDate(_d.EndDate, " - ")), ("Term:", $"{_d.TermMonths} months"));
            InfoRow(_w, _t, ("Billing Frequency:", _d.BillingFrequency), ("Billing Method:", _d.BillingMethod), ("Payment Method:", _d.PaymentMethod));
            InfoRow(_w, _t, ("Payment term:", _d.PaymentTerm), ("Currency:", _d.Currency), (null, null));

            OrderWording _wd = _d.Wording;
            if (Has(_wd.OrderNotes) || (_d.FreeMonths > 0 && Has(_wd.FreeMonthNote)))
            {
                Paragraph _notes = _w.Para(_before: 6, _after: 10, _indentCm: 0.2);
                _notes.Append(Run("Order Notes: ", _size: 9, _bold: true));
                if (Has(_wd.OrderNotes)) { Rich(_notes, _wd.OrderNotes, _size: 9); _notes.Append(Run(" ", _size: 9)); }
                if (_d.FreeMonths > 0 && Has(_wd.FreeMonthNote))
                    Rich(_notes, System.Text.RegularExpressions.Regex.Replace(_wd.FreeMonthNote, @"\b1 free months\b", "1 free month"), _size: 9, _allBold: true);
            }
        }

        private static void AddItems(Writer _w, OrderFormPdfData _d)
        {
            Table _t = _w.Table(new[] { 9.8, 2.7, 2.6, 2.7 }, _borders: true, _borderColor: Ink, _borderPt: 0.75, _padTopPt: 6, _padBottomPt: 10, _padLeftCm: 5 / 28.35, _padRightCm: 5 / 28.35);
            TableRow _head = _w.Row(_t, _header: true);
            string[] _titles = { "Description", "Unit Price", "Quantity", "Total" };
            for (int i = 0; i < _titles.Length; i++)
            {
                Paragraph _p = _w.Cell(_head, i, _fill: Navy, _padTopPt: 9, _padBottomPt: 9);
                if (i == 2) Align(_p, JustificationValues.Center);
                _p.Append(Run(_titles[i], _size: 10.5, _color: "FFFFFF"));
            }

            OrderWording _wd = _d.Wording;
            if (Has(_wd.ExtraItemTitle))
            {
                TableRow _r = _w.Row(_t);
                ItemText(_w, _r, Shade, _wd.ExtraItemTitle, _wd.ExtraItemDescription, null);
                _w.Cell(_r, 1, _fill: Shade).Append(Run($"₹ {OrderFormPdfDocument.Money(0m)}"));
                Paragraph _q = _w.Cell(_r, 2, _fill: Shade);
                Align(_q, JustificationValues.Center);
                _q.Append(Run(_d.IncludedItemQuantity?.ToString(CultureInfo.GetCultureInfo("en-IN")) ?? string.Empty));
                _w.Cell(_r, 3, _fill: Shade).Append(Run($"₹ {OrderFormPdfDocument.Money(0m)}"));
            }

            TableRow _p2 = _w.Row(_t);
            ItemText(_w, _p2, null, _wd.ProductTitle, _wd.ProductDescription, _wd.PeriodLine);
            _w.Cell(_p2, 1).Append(Run($"₹ {OrderFormPdfDocument.Money(_d.UnitPrice)}"));
            Paragraph _qty = _w.Cell(_p2, 2);
            Align(_qty, JustificationValues.Center);
            _qty.Append(Run(_d.Quantity.ToString(CultureInfo.GetCultureInfo("en-IN"))));
            _w.Cell(_p2, 3).Append(Run($"₹ {OrderFormPdfDocument.Money(_d.ListAmount)}"));
        }

        private static void AddTotals(Writer _w, OrderFormPdfData _d)
        {
            _w.Para(_after: 14);
            Table _t = _w.Table(new[] { 6.2, 4.2 }, _borders: true, _borderColor: Ink, _borderPt: 0.75, _padTopPt: 6, _padBottomPt: 6, _padLeftCm: 5 / 28.35, _padRightCm: 5 / 28.35, _alignRight: true);
            TotalRow(_w, _t, "Subtotal:", OrderFormPdfDocument.Fixed(_d.ListAmount), false, true);
            if (_d.DiscountAmount > 0 && _d.ListAmount > 0)
            {
                decimal _percent = Math.Round(_d.DiscountAmount * 100m / _d.ListAmount, 2);
                TotalRow(_w, _t, $"After {_percent.ToString("0.##", CultureInfo.InvariantCulture)}% Discount", OrderFormPdfDocument.Fixed(_d.SaleAmount), false, false);
            }
            TotalRow(_w, _t, $"Estimated Tax ({_d.GstPercent.ToString("0.##", CultureInfo.InvariantCulture)}% GST):", OrderFormPdfDocument.Fixed(_d.TaxAmount), false, false);
            TotalRow(_w, _t, $"Estimated Order Total ({_d.Currency ?? "INR"}):", OrderFormPdfDocument.Fixed(_d.TotalAmount), true, true);

            if (Has(_d.Wording.TaxInformation))
            {
                Paragraph _tax = _w.Para(_before: 10, _after: 10);
                Rich(_tax, _d.Wording.TaxInformation, _size: 7.5, _color: Muted);
            }
        }

        private static void AddSignatures(Writer _w, OrderFormPdfData _d, OrderFormPdfSettings _settings)
        {
            List<List<RichText.Run>> _clauses = RichText.Lines(_d.Wording.SignatureClauses);
            Table _band = _w.Table(new[] { ContentWidthCm }, _borders: true, _borderColor: Line, _borderPt: 0.5, _padLeftCm: 10 / 28.35, _padRightCm: 10 / 28.35);
            TableRow _title = _w.Row(_band, _cantSplit: true);
            _w.Cell(_title, 0, _fill: Band, _padTopPt: 8, _padBottomPt: 8).Append(Run("Authorized Signatures", _size: 11.5, _color: "FFFFFF"));
            if (_clauses.Count > 0)
            {
                TableRow _body = _w.Row(_band);
                Paragraph _placeholder = _w.Cell(_body, 0, _padTopPt: 6, _padBottomPt: 4);
                TableCell _cell = (TableCell)_placeholder.Parent!;
                _placeholder.Remove();
                for (int i = 0; i < _clauses.Count; i++)
                {
                    var _p = new Paragraph(new ParagraphProperties(
                        new Tabs(new TabStop { Val = TabStopValues.Left, Position = Twips(0.6) }),
                        new SpacingBetweenLines { After = "80" },
                        new Indentation { Left = Twips(0.6).ToString(CultureInfo.InvariantCulture), Hanging = Twips(0.6).ToString(CultureInfo.InvariantCulture) }));
                    _p.Append(Run($"{i + 1}.", _size: 8.5), new Run(new TabChar()));
                    // "Order Cancellation: ..." - a short lead-in before the first colon is bold (as in the PDF).
                    string _clause = string.Concat(_clauses[i].Select(r => r.Text));
                    int _colon = _clause.IndexOf(':');
                    bool _leadIn = _colon > 0 && _colon <= 30 && !_clause[.._colon].Contains("http", StringComparison.OrdinalIgnoreCase);
                    Runs(_p, _clauses[i], 8.5, null, _leadIn ? _colon + 1 : 0);
                    _cell.Append(_p);
                }
            }

            _w.Para(_after: 10);
            Table _boxes = _w.Table(new[] { 8.1, 1.6, 8.1 }, _borders: false);
            TableRow _row = _w.Row(_boxes, _cantSplit: true);
            SignatureBox(_w, _row, 0, "Customer Signature", "[Authorized Signatory Name]", "[Designation]", "[Date]");
            _w.Cell(_row, 1);
            SignatureBox(_w, _row, 2, $"{_settings.SignatureName} Signature", "[Authorized Signatory]", _settings.CompanyName, $"[{OrderFormPdfDocument.LongDate(_d.OrderDate, " ")}]");

            List<List<RichText.Run>> _footer = RichText.Lines(_d.Wording.Footer);
            if (_footer.Count > 0)
            {
                _w.HRule(0.5, Line, 10, 6);
                foreach (List<RichText.Run> _line in _footer)
                {
                    Paragraph _p = _w.Para(_after: 3);
                    Align(_p, JustificationValues.Center);
                    Runs(_p, _line, 8.5, Muted);
                }
            }
        }

        // ---------------- pieces ----------------

        private static void InfoRow(Writer _w, Table _t, params (string label, string value)[] _cells)
        {
            TableRow _r = _w.Row(_t);
            for (int i = 0; i < _cells.Length; i++)
            {
                Paragraph _p = _w.Cell(_r, i);
                if (_cells[i].label == null) continue;
                _p.Append(Run(_cells[i].label + " ", _bold: true), Run(_cells[i].value ?? "-"));
            }
        }

        private static void ItemText(Writer _w, TableRow _r, string _fill, string _title, string _description, string _period)
        {
            Paragraph _first = _w.Cell(_r, 0, _fill: _fill);
            TableCell _cell = _r.Elements<TableCell>().Last();
            bool _used = false;
            Paragraph Next(int _before = 0, int _after = 0)
            {
                Paragraph _p = _used ? _cell.AppendChild(new Paragraph()) : _first;
                _used = true;
                _p.PrependChild(new ParagraphProperties(new SpacingBetweenLines { Before = (_before * 20).ToString(CultureInfo.InvariantCulture), After = (_after * 20).ToString(CultureInfo.InvariantCulture) }));
                return _p;
            }
            if (Has(_title)) Rich(Next(_after: 2), _title, _size: 10.5);
            if (Has(_description)) Rich(Next(), _description, _size: 9);
            if (Has(_period)) Rich(Next(_before: 4), _period, _size: 9, _allBold: true);
        }

        private static void TotalRow(Writer _w, Table _t, string _label, string _value, bool _bold, bool _boldValue)
        {
            TableRow _r = _w.Row(_t);
            _w.Cell(_r, 0).Append(Run(_label, _size: 10, _bold: _bold));
            Paragraph _v = _w.Cell(_r, 1);
            Align(_v, JustificationValues.Right);
            _v.Append(Run(_value, _size: 10, _bold: _bold || _boldValue));
        }

        private static void SignatureBox(Writer _w, TableRow _row, int _index, string _title, params string[] _lines)
        {
            Paragraph _t = _w.Cell(_row, _index, _boxBorder: Line);
            TableCell _cell = _row.Elements<TableCell>().Last();
            _t.PrependChild(new ParagraphProperties(new SpacingBetweenLines { Before = "200", After = "480" }, new Indentation { Left = "200", Right = "200" }));
            _t.Append(Run(_title, _bold: true));
            var _line = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "120" }, new Indentation { Left = "200", Right = "200" }), Run(new string('_', 34)));
            _cell.Append(_line);
            for (int i = 0; i < _lines.Length; i++)
                _cell.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = i == _lines.Length - 1 ? "200" : "120" }, new Indentation { Left = "200", Right = "200" }), Run(_lines[i] ?? string.Empty)));
        }

        /// <summary>A wording section with its formatting; new lines become line breaks.</summary>
        private static void Rich(Paragraph _p, string _markup, double _size = 9.5, string _color = null, bool _allBold = false)
        {
            List<List<RichText.Run>> _lines = RichText.Parse(RichText.Clean(_markup));
            for (int i = 0; i < _lines.Count; i++)
            {
                if (i > 0) _p.Append(Break());
                Runs(_p, _lines[i], _size, _color, _allBold ? int.MaxValue : 0);
            }
        }

        /// <summary>The runs of one line; the first _boldChars characters are bold as well.</summary>
        private static void Runs(Paragraph _p, IEnumerable<RichText.Run> _runs, double _size, string _color, int _boldChars = 0)
        {
            int _at = 0;
            foreach (RichText.Run _run in _runs)
            {
                string _text = _run.Text;
                if (_boldChars > _at && _boldChars < _at + _text.Length)
                {
                    int _cut = _boldChars - _at;
                    _p.Append(RichRun(_run with { Text = _text[.._cut], Bold = true }, _size, _color));
                    _p.Append(RichRun(_run with { Text = _text[_cut..] }, _size, _color));
                }
                else _p.Append(RichRun(_at < _boldChars ? _run with { Bold = true } : _run, _size, _color));
                _at += _text.Length;
            }
        }

        private static Run RichRun(RichText.Run _run, double _size, string _color)
            => Run(_run.Text, _size, _run.Bold, _run.Italic, _run.Underline, _run.Color?.TrimStart('#') ?? _color, _run.Background?.TrimStart('#'));

        /// <summary>A run of text. Property order follows the Word schema.</summary>
        private static Run Run(string _text, double _size = 9.5, bool _bold = false, bool _italic = false, bool _underline = false, string _color = null, string _highlight = null)
        {
            var _props = new RunProperties();
            if (_bold) _props.Append(new Bold());
            if (_italic) _props.Append(new Italic());
            if (_color != null) _props.Append(new Color { Val = _color });
            _props.Append(new FontSize { Val = HalfPoints(_size) }, new FontSizeComplexScript { Val = HalfPoints(_size) });
            if (_underline) _props.Append(new Underline { Val = UnderlineValues.Single });
            if (_highlight != null) _props.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = _highlight });
            return new Run(_props, new Text(_text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
        }

        private static Run Break() => new Run(new Break());

        private static void Align(Paragraph _p, JustificationValues _value)
        {
            ParagraphProperties _pp = _p.ParagraphProperties ?? _p.PrependChild(new ParagraphProperties());
            _pp.Append(new Justification { Val = _value });
        }

        private static bool Has(string _value) => !string.IsNullOrWhiteSpace(_value) && RichText.Plain(_value).Trim().Length > 0;

        /// <summary>Adds paragraphs, tables and pictures to the body.</summary>
        private sealed class Writer
        {
            private readonly MainDocumentPart _main;
            private readonly Body _body;
            private uint _pictureId = 1;

            public Writer(MainDocumentPart __main)
            {
                _main = __main;
                _body = __main.Document.Body!;
            }

            public Paragraph Para(double _before = 0, double _after = 0, double _indentCm = 0)
            {
                var _pp = new ParagraphProperties(new SpacingBetweenLines
                {
                    Before = ((int)(_before * 20)).ToString(CultureInfo.InvariantCulture),
                    After = ((int)(_after * 20)).ToString(CultureInfo.InvariantCulture)
                });
                if (_indentCm > 0) _pp.Append(new Indentation { Left = Twips(_indentCm).ToString(CultureInfo.InvariantCulture) });
                return _body.AppendChild(new Paragraph(_pp));
            }

            /// <summary>A horizontal line: an empty paragraph with a bottom border.</summary>
            public void HRule(double _widthPt, string _color, double _before, double _after, double _indentCm = 0)
            {
                var _pp = new ParagraphProperties(
                    new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = (uint)Math.Max(2, Math.Round(_widthPt * 8)), Color = _color, Space = 1U }),
                    new SpacingBetweenLines { Before = ((int)(_before * 20)).ToString(CultureInfo.InvariantCulture), After = ((int)(_after * 20)).ToString(CultureInfo.InvariantCulture), Line = "20", LineRule = LineSpacingRuleValues.Exact });
                if (_indentCm > 0) _pp.Append(new Indentation { Left = Twips(_indentCm).ToString(CultureInfo.InvariantCulture), Right = Twips(_indentCm).ToString(CultureInfo.InvariantCulture) });
                _body.AppendChild(new Paragraph(_pp));
            }

            public Table Table(double[] _widthsCm, bool _borders, string _borderColor = Ink, double _borderPt = 0.75, double _padTopPt = 0, double _padBottomPt = 0,
                               double _padLeftCm = 0.19, double _padRightCm = 0.19, bool _alignRight = false)
            {
                uint _size = (uint)Math.Max(2, Math.Round(_borderPt * 8));
                BorderValues _kind = _borders ? BorderValues.Single : BorderValues.None;
                var _tp = new TableProperties(new TableWidth { Width = _widthsCm.Sum(Twips).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa });
                if (_alignRight) _tp.Append(new TableJustification { Val = TableRowAlignmentValues.Right });
                _tp.Append(
                    new TableBorders(
                        new TopBorder { Val = _kind, Size = _size, Color = _borderColor }, new LeftBorder { Val = _kind, Size = _size, Color = _borderColor },
                        new BottomBorder { Val = _kind, Size = _size, Color = _borderColor }, new RightBorder { Val = _kind, Size = _size, Color = _borderColor },
                        new InsideHorizontalBorder { Val = _kind, Size = _size, Color = _borderColor }, new InsideVerticalBorder { Val = _kind, Size = _size, Color = _borderColor }),
                    new TableLayout { Type = TableLayoutValues.Fixed },
                    new TableCellMarginDefault(
                        new TopMargin { Width = ((int)(_padTopPt * 20)).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                        new TableCellLeftMargin { Width = (short)Twips(_padLeftCm), Type = TableWidthValues.Dxa },
                        new BottomMargin { Width = ((int)(_padBottomPt * 20)).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                        new TableCellRightMargin { Width = (short)Twips(_padRightCm), Type = TableWidthValues.Dxa }));
                var _grid = new TableGrid(_widthsCm.Select(w => new GridColumn { Width = Twips(w).ToString(CultureInfo.InvariantCulture) }));
                return _body.AppendChild(new Table(_tp, _grid));
            }

            public TableRow Row(Table _t, bool _header = false, bool _cantSplit = false)
            {
                var _trp = new TableRowProperties();
                if (_cantSplit) _trp.Append(new CantSplit());
                if (_header) _trp.Append(new TableHeader());
                return _t.AppendChild(new TableRow(_trp));
            }

            /// <summary>Adds cell _index to the row and returns its (first) paragraph.</summary>
            public Paragraph Cell(TableRow _r, int _index, string _fill = null, bool _vCenter = false, double? _padTopPt = null, double? _padBottomPt = null, string _boxBorder = null)
            {
                Table _t = (Table)_r.Parent!;
                GridColumn _col = _t.GetFirstChild<TableGrid>()!.Elements<GridColumn>().ElementAt(_index);
                var _tcp = new TableCellProperties(new TableCellWidth { Width = _col.Width!.Value, Type = TableWidthUnitValues.Dxa });
                if (_boxBorder != null)
                    _tcp.Append(new TableCellBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4U, Color = _boxBorder }, new LeftBorder { Val = BorderValues.Single, Size = 4U, Color = _boxBorder },
                        new BottomBorder { Val = BorderValues.Single, Size = 4U, Color = _boxBorder }, new RightBorder { Val = BorderValues.Single, Size = 4U, Color = _boxBorder }));
                if (_fill != null) _tcp.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = _fill });
                if (_padTopPt.HasValue || _padBottomPt.HasValue)
                    _tcp.Append(new TableCellMargin(
                        new TopMargin { Width = ((int)((_padTopPt ?? 0) * 20)).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = ((int)((_padBottomPt ?? 0) * 20)).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa }));
                if (_vCenter) _tcp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
                var _p = new Paragraph();
                _r.AppendChild(new TableCell(_tcp, _p));
                return _p;
            }

            /// <summary>A PNG at the given width (height kept in proportion).</summary>
            public Run Image(string _path, double _widthCm)
            {
                byte[] _bytes = File.ReadAllBytes(_path);
                ImagePart _part = _main.AddImagePart(ImagePartType.Png);
                using (var _ms = new MemoryStream(_bytes)) _part.FeedData(_ms);
                string _relId = _main.GetIdOfPart(_part);
                // PNG size from the IHDR chunk (bytes 16-23, big-endian).
                int _pw = (_bytes[16] << 24) | (_bytes[17] << 16) | (_bytes[18] << 8) | _bytes[19];
                int _ph = (_bytes[20] << 24) | (_bytes[21] << 16) | (_bytes[22] << 8) | _bytes[23];
                long _cx = (long)(_widthCm * 360000);
                long _cy = _pw > 0 ? _cx * _ph / _pw : _cx;
                uint _id = _pictureId++;
                var _inline = new DW.Inline(
                    new DW.Extent { Cx = _cx, Cy = _cy },
                    new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties { Id = _id, Name = $"Picture {_id}" },
                    new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(new A.GraphicData(new PIC.Picture(
                        new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = 0U, Name = Path.GetFileName(_path) }, new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(new A.Blip { Embed = _relId }, new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = _cx, Cy = _cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U };
                return new Run(new Drawing(_inline));
            }

            /// <summary>A mailto: link, blue and underlined.</summary>
            public Hyperlink Email(string _email, double _size)
            {
                HyperlinkRelationship _rel = _main.AddHyperlinkRelationship(new Uri("mailto:" + _email.Trim()), true);
                return new Hyperlink(OrderFormWordDocument.Run(_email.Trim(), _size, _underline: true, _color: Link)) { Id = _rel.Id };
            }
        }
    }
}
