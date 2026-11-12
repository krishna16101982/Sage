using Microsoft.Data.SqlClient;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.AppFunctions;

public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly IDatabaseSettings _databaseSettings;

    public SqlConnectionFactory(IDatabaseSettings __databaseSettings)
    {
        _databaseSettings = __databaseSettings;
    }

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_databaseSettings.ConnectionString))
            throw new DatabaseException("Database connection string is not configured.");

        var connection = new SqlConnection(_databaseSettings.ConnectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            return connection;
        }
        catch (SqlException ex)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new DatabaseException("Unable to connect to the database.", ex.Number, ex);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
