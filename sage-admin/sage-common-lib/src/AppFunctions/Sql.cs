using System.Data;
using Microsoft.Data.SqlClient;
using SageCommonLib.AppValidations;

namespace SageCommonLib.AppFunctions
{
    /// <summary>
    /// Short helpers for calling stored procedures and reading their rows, shared by both APIs. Use them with
    /// <c>using static SageCommonLib.AppFunctions.Sql;</c> so a call reads <c>P("@Name", _name)</c> and <c>S(r, 3)</c>.
    /// Values always travel as parameters: never put a value into the SQL text.
    /// </summary>
    public static class Sql
    {
        // ---- Parameters ----

        /// <summary>An input parameter; null becomes SQL NULL.</summary>
        public static SqlParameter P(string _name, object? _value) => new SqlParameter(_name, _value ?? DBNull.Value);

        /// <summary>A DATETIME2 parameter (UTC times).</summary>
        public static SqlParameter Dt(string _name, DateTime _value) => new SqlParameter(_name, SqlDbType.DateTime2) { Value = _value };

        /// <summary>A DATE parameter; null becomes SQL NULL.</summary>
        public static SqlParameter Date(string _name, DateOnly? _value)
            => new SqlParameter(_name, SqlDbType.Date) { Value = _value.HasValue ? _value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value };

        /// <summary>A DECIMAL(18,2) amount; null becomes SQL NULL.</summary>
        public static SqlParameter Money(string _name, decimal? _value)
            => new SqlParameter(_name, SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = _value.HasValue ? _value.Value : DBNull.Value };

        /// <summary>An OUTPUT INT parameter (e.g. @TotalCount); read it with <see cref="OutInt"/> after the call.</summary>
        public static SqlParameter IntOut(string _name) => new SqlParameter(_name, SqlDbType.Int) { Direction = ParameterDirection.Output };

        /// <summary>An OUTPUT DECIMAL(18,2) parameter (e.g. a total); read it with <see cref="OutMoney"/> after the call.</summary>
        public static SqlParameter MoneyOut(string _name) => new SqlParameter(_name, SqlDbType.Decimal) { Precision = 18, Scale = 2, Direction = ParameterDirection.Output };

        /// <summary>The value of an OUTPUT INT parameter, 0 when NULL.</summary>
        public static int OutInt(SqlParameter _parameter) => _parameter.Value is int _value ? _value : 0;

        /// <summary>The value of an OUTPUT DECIMAL parameter, 0 when NULL.</summary>
        public static decimal OutMoney(SqlParameter _parameter) => _parameter.Value is decimal _value ? _value : 0m;

        // ---- Reading rows ----

        /// <summary>A nullable text column.</summary>
        public static string? S(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

        /// <summary>A nullable DATETIME2 column holding UTC, marked as UTC.</summary>
        public static DateTime? Utc(SqlDataReader r, int i) => r.IsDBNull(i) ? null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);

        // ---- Text ----

        /// <summary>Trims, strips HTML tags and turns empty text into null (free text such as names and notes).</summary>
        public static string? Clean(string? _value) => string.IsNullOrWhiteSpace(_value) ? null : Sanitization.Sanitize(_value.Trim());

        /// <summary>Cuts text to fit a column (IP addresses, user agents).</summary>
        public static string? Truncate(string? _value, int _max) => string.IsNullOrEmpty(_value) || _value.Length <= _max ? _value : _value.Substring(0, _max);
    }
}
