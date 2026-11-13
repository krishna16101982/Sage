using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Builds a CSV file that opens correctly in Excel: UTF-8 with a byte-order mark, CRLF line ends, fields quoted when
    /// needed. Text that starts with = + - @ or a tab is prefixed with ' so a spreadsheet never runs it as a formula
    /// (customer names and notes are typed by users). Numbers and dates are written by the caller and are not prefixed.
    /// </summary>
    public class CsvBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public CsvBuilder(params string[] _headers)
        {
            Row(_headers.Select(Text).ToArray());
        }

        public int RowCount { get; private set; } = -1;

        /// <summary>A user-entered or free text value.</summary>
        public static string Text(string _value)
        {
            if (string.IsNullOrEmpty(_value)) return string.Empty;
            return "=+-@\t\r".IndexOf(_value[0]) >= 0 ? "'" + _value : _value;
        }

        public static string Number(decimal _value) => _value.ToString("0.00", CultureInfo.InvariantCulture);
        public static string Number(int _value) => _value.ToString(CultureInfo.InvariantCulture);
        public static string Date(DateOnly _value) => _value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public static string Date(DateTime? _utc) => _utc.HasValue ? _utc.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : string.Empty;

        /// <summary>Adds one row of already-formatted values (use Text / Number / Date).</summary>
        public CsvBuilder Row(params string[] _values)
        {
            _sb.Append(string.Join(",", _values.Select(Quote))).Append("\r\n");
            RowCount++;
            return this;
        }

        private static string Quote(string _value)
        {
            _value ??= string.Empty;
            return _value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + _value.Replace("\"", "\"\"") + "\"" : _value;
        }

        public byte[] ToBytes()
        {
            byte[] _bom = Encoding.UTF8.GetPreamble();
            byte[] _body = Encoding.UTF8.GetBytes(_sb.ToString());
            return _bom.Concat(_body).ToArray();
        }

        public override string ToString() => _sb.ToString();
    }

    /// <summary>A file for the controller to send.</summary>
    public class ExportFile
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public int RowCount { get; set; }
    }
}
