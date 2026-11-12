using Microsoft.Data.SqlClient;
using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.AppFunctions.Interfaces;

/// <summary>Async, parameterized SQL access. Callers never build SQL from user input.</summary>
public interface IDatabaseFunctions
{
    Task<int> ExecuteNonQueryAsync(string sql, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default);
    Task<T?> ExecuteScalarAsync<T>(string sql, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default);
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default);
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, Func<SqlDataReader, T> map, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default);

    /// <summary>Runs work in one transaction; rolls back on any exception.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> work, CancellationToken ct = default);

    // ---- Write wrappers (same shape as ixcheck ExecuteInsert/ExecuteUpdate/ExecuteDelete) ----
    // They never throw for expected database errors: a duplicate key, a foreign key conflict or an SQL failure comes back as
    // a failed IFunctionReturn (409 / 500). Values must be passed as parameters, never concatenated into the SQL text.

    /// <summary>INSERT. retValue = the new identity value (0 when the table has none).</summary>
    Task<(IFunctionReturn functionReturn, long retValue)> ExecuteInsertAsync(string sql, IEnumerable<SqlParameter>? parameters, string methodName, string? successMessage = null, CancellationToken ct = default);

    /// <summary>UPDATE (or a multi-statement batch). retValue = rows affected.</summary>
    Task<(IFunctionReturn functionReturn, long retValue)> ExecuteUpdateAsync(string sql, IEnumerable<SqlParameter>? parameters, string methodName, string? successMessage = null, CancellationToken ct = default);

    /// <summary>DELETE. retValue = rows affected.</summary>
    Task<(IFunctionReturn functionReturn, long retValue)> ExecuteDeleteAsync(string sql, IEnumerable<SqlParameter>? parameters, string methodName, string? successMessage = null, CancellationToken ct = default);

    /// <summary>Run checkSql (a query returning a count); if it is above zero fail with 409 and existsMessage, otherwise run the INSERT.</summary>
    Task<(IFunctionReturn functionReturn, long retValue)> ExecuteCheckExistsAndInsertAsync(string checkSql, string insertSql, IEnumerable<SqlParameter>? parameters, string methodName, string existsMessage, string? successMessage = null, CancellationToken ct = default);

    // ---- Stored procedures (procedureName is a fixed name such as "dbo.usp_Module_Create", never user input) ----
    // A procedure signals expected business errors with THROW: 50404 maps to 404 and 50409 to 409; any other user error number
    // comes back as a DatabaseException carrying SqlErrorNumber.

    /// <summary>Runs a procedure. Returns rows affected as reported by SQL Server (-1 when the procedure uses SET NOCOUNT ON).</summary>
    Task<int> ExecuteProcedureAsync(string procedureName, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default);

    /// <summary>Runs a procedure and returns the first column of its first row.</summary>
    Task<T?> ExecuteProcedureScalarAsync<T>(string procedureName, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default);

    /// <summary>Runs a procedure and maps its first result set. OUTPUT parameters hold their values when this returns.</summary>
    Task<IReadOnlyList<T>> QueryProcedureAsync<T>(string procedureName, Func<SqlDataReader, T> map, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default);

    /// <summary>Write wrapper for a procedure: never throws for expected database errors (409 / 404 / 500 come back as a failed IFunctionReturn).
    /// retValue = the procedure's scalar result (by convention the new id for a create, rows affected for an update).</summary>
    Task<(IFunctionReturn functionReturn, long retValue)> ExecuteProcedureWriteAsync(string procedureName, IEnumerable<SqlParameter>? parameters, string methodName, string? successMessage = null, CancellationToken ct = default);

    /// <summary>True when the database answers a trivial query.</summary>
    Task<bool> PingAsync(CancellationToken ct = default);
}
