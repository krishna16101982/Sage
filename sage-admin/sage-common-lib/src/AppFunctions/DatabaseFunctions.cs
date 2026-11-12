using System.Data;
using Microsoft.Data.SqlClient;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.AppFunctions;

public class DatabaseFunctions : IDatabaseFunctions
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IDatabaseSettings _databaseSettings;

    public DatabaseFunctions(ISqlConnectionFactory __connectionFactory, IDatabaseSettings __databaseSettings)
    {
        _connectionFactory = __connectionFactory;
        _databaseSettings = __databaseSettings;
    }

    #region Core execution (text or stored procedure)

    private Task<int> NonQueryCoreAsync(string commandText, CommandType commandType, IEnumerable<SqlParameter>? parameters, CancellationToken ct)
        => WithRetryAsync(async () =>
        {
            await using var conn = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var cmd = CreateCommand(conn, null, commandText, parameters, commandType);
            try { return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false); }
            finally { cmd.Parameters.Clear(); }
        });

    private Task<T?> ScalarCoreAsync<T>(string commandText, CommandType commandType, IEnumerable<SqlParameter>? parameters, CancellationToken ct)
        => WithRetryAsync(async () =>
        {
            await using var conn = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var cmd = CreateCommand(conn, null, commandText, parameters, commandType);
            object? result;
            try { result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false); }
            finally { cmd.Parameters.Clear(); }
            if (result is null || result is DBNull) return default;
            if (result is T typed) return typed;
            return (T)Convert.ChangeType(result, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
        });

    private Task<IReadOnlyList<T>> QueryCoreAsync<T>(string commandText, CommandType commandType, Func<SqlDataReader, T> map, IEnumerable<SqlParameter>? parameters, CancellationToken ct)
        => WithRetryAsync<IReadOnlyList<T>>(async () =>
        {
            await using var conn = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var cmd = CreateCommand(conn, null, commandText, parameters, commandType);
            var list = new List<T>();
            try
            {
                // OUTPUT parameters are filled in only once the reader is closed, so it is disposed before the parameters are released.
                await using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false)) list.Add(map(reader));
                }
            }
            finally { cmd.Parameters.Clear(); }
            return list;
        });

    #endregion

    public Task<int> ExecuteNonQueryAsync(string sql, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default)
        => NonQueryCoreAsync(sql, CommandType.Text, parameters, ct);

    public Task<T?> ExecuteScalarAsync<T>(string sql, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default)
        => ScalarCoreAsync<T>(sql, CommandType.Text, parameters, ct);

    public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default)
        => QueryCoreAsync(sql, CommandType.Text, map, parameters, ct);

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, Func<SqlDataReader, T> map, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default)
    {
        var rows = await QueryAsync(sql, map, parameters, ct).ConfigureAwait(false);
        return rows.Count == 0 ? default : rows[0];
    }

    #region Stored procedures

    public Task<int> ExecuteProcedureAsync(string procedureName, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default)
        => NonQueryCoreAsync(procedureName, CommandType.StoredProcedure, parameters, ct);

    public Task<T?> ExecuteProcedureScalarAsync<T>(string procedureName, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default)
        => ScalarCoreAsync<T>(procedureName, CommandType.StoredProcedure, parameters, ct);

    public Task<IReadOnlyList<T>> QueryProcedureAsync<T>(string procedureName, Func<SqlDataReader, T> map, IEnumerable<SqlParameter>? parameters = null, CancellationToken ct = default)
        => QueryCoreAsync(procedureName, CommandType.StoredProcedure, map, parameters, ct);

    public async Task<(IFunctionReturn functionReturn, long retValue)> ExecuteProcedureWriteAsync(string procedureName, IEnumerable<SqlParameter>? parameters, string methodName, string? successMessage = null, CancellationToken ct = default)
    {
        try
        {
            long value = await ScalarCoreAsync<long>(procedureName, CommandType.StoredProcedure, Clone(parameters), ct).ConfigureAwait(false);
            return (Success(methodName, successMessage), value);
        }
        catch (AppException ex)
        {
            return (FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, methodName), 0);
        }
    }

    #endregion

    public async Task<T> ExecuteInTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> work, CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var result = await work(conn, tx).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return result;
        }
        catch (SqlException ex)
        {
            await SafeRollbackAsync(tx).ConfigureAwait(false);
            throw Map(ex);
        }
        catch
        {
            await SafeRollbackAsync(tx).ConfigureAwait(false);
            throw;
        }
    }

    #region Write wrappers

    /// <summary>Each command needs its own parameter objects (a SqlParameter belongs to one command).</summary>
    private static List<SqlParameter> Clone(IEnumerable<SqlParameter>? parameters)
        => parameters == null ? new List<SqlParameter>() : parameters.Select(p => new SqlParameter(p.ParameterName, p.Value) { SqlDbType = p.SqlDbType, Size = p.Size, Direction = p.Direction }).ToList();

    private static IFunctionReturn Success(string methodName, string? message)
    {
        var fr = new FunctionReturn { MethodName = methodName };
        if (!string.IsNullOrEmpty(message)) fr.Message.Add(message);
        return fr;
    }

    private async Task<(IFunctionReturn functionReturn, long retValue)> RunWriteAsync(string sql, IEnumerable<SqlParameter>? parameters, string methodName, string? successMessage, bool returnIdentity, CancellationToken ct)
    {
        try
        {
            long value;
            if (returnIdentity)
                value = await ExecuteScalarAsync<long>(sql + "; SELECT CAST(SCOPE_IDENTITY() AS bigint);", Clone(parameters), ct).ConfigureAwait(false);
            else
                value = await ExecuteNonQueryAsync(sql, Clone(parameters), ct).ConfigureAwait(false);
            return (Success(methodName, successMessage), value);
        }
        catch (AppException ex)
        {
            return (FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, methodName), 0);
        }
    }

    public Task<(IFunctionReturn functionReturn, long retValue)> ExecuteInsertAsync(string sql, IEnumerable<SqlParameter>? parameters, string methodName, string? successMessage = null, CancellationToken ct = default)
        => RunWriteAsync(sql, parameters, methodName, successMessage, true, ct);

    public Task<(IFunctionReturn functionReturn, long retValue)> ExecuteUpdateAsync(string sql, IEnumerable<SqlParameter>? parameters, string methodName, string? successMessage = null, CancellationToken ct = default)
        => RunWriteAsync(sql, parameters, methodName, successMessage, false, ct);

    public Task<(IFunctionReturn functionReturn, long retValue)> ExecuteDeleteAsync(string sql, IEnumerable<SqlParameter>? parameters, string methodName, string? successMessage = null, CancellationToken ct = default)
        => RunWriteAsync(sql, parameters, methodName, successMessage, false, ct);

    public async Task<(IFunctionReturn functionReturn, long retValue)> ExecuteCheckExistsAndInsertAsync(string checkSql, string insertSql, IEnumerable<SqlParameter>? parameters, string methodName, string existsMessage, string? successMessage = null, CancellationToken ct = default)
    {
        try
        {
            int count = await ExecuteScalarAsync<int>(checkSql, Clone(parameters), ct).ConfigureAwait(false);
            if (count > 0)
                return (FunctionReturn.Fail(Constants.ReturnMessageTypes.Conflict, existsMessage, Constants.HttpStatusCodes.Conflict, methodName), 0);
        }
        catch (AppException ex)
        {
            return (FunctionReturn.Fail(ex.MessageType, ex.Message, ex.HttpStatusCode, methodName), 0);
        }
        // A concurrent insert can still win the race; the unique index then turns into the same 409 below.
        var result = await RunWriteAsync(insertSql, parameters, methodName, successMessage, true, ct).ConfigureAwait(false);
        if (!result.functionReturn.Status && result.functionReturn.HttpStatusCode == Constants.HttpStatusCodes.Conflict)
            result.functionReturn.Message = new List<string> { existsMessage };
        return result;
    }

    #endregion

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try { return await ExecuteScalarAsync<int>("SELECT 1", null, ct).ConfigureAwait(false) == 1; }
        catch (DatabaseException) { return false; }
    }

    /// <summary>Helper for transactional callers to build commands on the shared connection.</summary>
    public SqlCommand CreateCommand(SqlConnection conn, SqlTransaction? tx, string sql, IEnumerable<SqlParameter>? parameters)
        => CreateCommand(conn, tx, sql, parameters, CommandType.Text);

    /// <summary>Same as above; commandType = StoredProcedure when commandText is a procedure name.</summary>
    public SqlCommand CreateCommand(SqlConnection conn, SqlTransaction? tx, string commandText, IEnumerable<SqlParameter>? parameters, CommandType commandType)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = commandText;
        cmd.CommandType = commandType;
        cmd.CommandTimeout = _databaseSettings.CommandTimeoutSeconds;
        cmd.Transaction = tx;
        if (parameters is not null) foreach (var p in parameters) cmd.Parameters.Add(p);
        return cmd;
    }

    private async Task<T> WithRetryAsync<T>(Func<Task<T>> action)
    {
        var attempt = 0;
        while (true)
        {
            try { return await action().ConfigureAwait(false); }
            catch (SqlException ex) when (ex.IsTransient && attempt < _databaseSettings.MaxRetryCount)
            {
                attempt++;
                await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))).ConfigureAwait(false);
            }
            catch (SqlException ex) { throw Map(ex); }
        }
    }

    private static async Task SafeRollbackAsync(SqlTransaction tx)
    {
        try { await tx.RollbackAsync().ConfigureAwait(false); } catch { /* connection may already be broken */ }
    }

    /// <summary>Maps SQL errors to app exceptions without leaking server details to clients.</summary>
    public static AppException Map(SqlException ex) => ex.Number switch
    {
        DatabaseConstants.SqlErrorNumbers.UniqueConstraintViolation or DatabaseConstants.SqlErrorNumbers.UniqueIndexViolation
            => new ConflictException("A record with the same unique value already exists."),
        DatabaseConstants.SqlErrorNumbers.ForeignKeyViolation => new ConflictException("The operation conflicts with related data."),
        // Our own procedures THROW these with a message written for the user (never SQL Server text), so it is passed on.
        DatabaseConstants.SqlErrorNumbers.ProcedureAlreadyExists => new ConflictException(ex.Message),
        DatabaseConstants.SqlErrorNumbers.ProcedureNotFound => new NotFoundException(ex.Message),
        DatabaseConstants.SqlErrorNumbers.ProcedureInvalidInput => new AppException(ex.Message, Constants.HttpStatusCodes.BadRequest, Constants.ReturnMessageTypes.ValidationError),
        _ => new DatabaseException("A database error occurred.", ex.Number, ex)
    };
}
