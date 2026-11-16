using System.Data;

namespace SageCommonLib.AppFunctions
{
    /// <summary>
    /// Reads a column of a DataRow by name, NULL-safe: <c>_row.GetText("Name")</c>, <c>_row.GetGuid("PublicId")</c>,
    /// <c>_row.GetNullableUtc("LastLoginAtUtc")</c>. Dates stored as UTC come back marked as UTC.
    /// </summary>
    public static class DataRowExtensions
    {
        /// <summary>The first row, or null when the table is empty.</summary>
        public static DataRow? FirstRow(this DataTable _table) => _table.Rows.Count == 0 ? null : _table.Rows[0];

        /// <summary>Every row turned into an object: <c>_dataTable.MapRows(MapCustomer)</c>.</summary>
        public static List<T> MapRows<T>(this DataTable _table, Func<DataRow, T> _map)
        {
            var _items = new List<T>(_table.Rows.Count);
            foreach (DataRow _row in _table.Rows) _items.Add(_map(_row));
            return _items;
        }

        private static bool IsNull(DataRow _row, string _column) => _row.IsNull(_column);

        /// <summary>Text, or null when NULL.</summary>
        public static string? GetText(this DataRow _row, string _column) => IsNull(_row, _column) ? null : Convert.ToString(_row[_column]);

        public static Guid GetGuid(this DataRow _row, string _column) => (Guid)_row[_column];
        public static Guid? GetNullableGuid(this DataRow _row, string _column) => IsNull(_row, _column) ? null : (Guid)_row[_column];

        public static int GetInt(this DataRow _row, string _column) => IsNull(_row, _column) ? 0 : Convert.ToInt32(_row[_column]);
        public static int? GetNullableInt(this DataRow _row, string _column) => IsNull(_row, _column) ? null : Convert.ToInt32(_row[_column]);

        public static long GetLong(this DataRow _row, string _column) => IsNull(_row, _column) ? 0 : Convert.ToInt64(_row[_column]);
        public static long? GetNullableLong(this DataRow _row, string _column) => IsNull(_row, _column) ? null : Convert.ToInt64(_row[_column]);

        public static decimal GetDecimal(this DataRow _row, string _column) => IsNull(_row, _column) ? 0m : Convert.ToDecimal(_row[_column]);
        public static decimal? GetNullableDecimal(this DataRow _row, string _column) => IsNull(_row, _column) ? null : Convert.ToDecimal(_row[_column]);

        public static bool GetBool(this DataRow _row, string _column) => !IsNull(_row, _column) && Convert.ToBoolean(_row[_column]);

        /// <summary>A DATETIME2 column holding UTC, marked as UTC.</summary>
        public static DateTime GetUtc(this DataRow _row, string _column) => DateTime.SpecifyKind((DateTime)_row[_column], DateTimeKind.Utc);
        public static DateTime? GetNullableUtc(this DataRow _row, string _column) => IsNull(_row, _column) ? null : DateTime.SpecifyKind((DateTime)_row[_column], DateTimeKind.Utc);

        /// <summary>A DATE column.</summary>
        public static DateOnly GetDate(this DataRow _row, string _column) => DateOnly.FromDateTime((DateTime)_row[_column]);
        public static DateOnly? GetNullableDate(this DataRow _row, string _column) => IsNull(_row, _column) ? null : DateOnly.FromDateTime((DateTime)_row[_column]);
    }
}
