using Microsoft.Data.SqlClient;

namespace SageAdminApi.Tests.Infrastructure;

/// <summary>
/// Creates a throw-away database named SageAdmin_Test_{guid}, applies every script in backend/database,
/// and drops the database afterwards. Refuses to run against anything that looks like production.
/// Server comes from env SAGE_TEST_SQL_SERVER (default localhost\SQLEXPRESS, Windows authentication).
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string CollectionName = "SqlServer";
    private string _serverConnection = string.Empty;

    public string DatabaseName { get; } = $"SageAdmin_Test_{Guid.NewGuid():N}";
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("SAGE_TEST_SQL_SERVER") ?? @"localhost\SQLEXPRESS";
        if (server.Contains("prod", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to run destructive tests against a production-looking server.");

        _serverConnection = $"Server={server};Database=master;Integrated Security=true;TrustServerCertificate=true;Encrypt=true;Connect Timeout=15";
        await ExecAsync(_serverConnection, $"CREATE DATABASE [{DatabaseName}]");
        ConnectionString = $"Server={server};Database={DatabaseName};Integrated Security=true;TrustServerCertificate=true;Encrypt=true;Connect Timeout=15";
        await SqlScriptRunner.ApplyAllAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (!DatabaseName.StartsWith("SageAdmin_Test_", StringComparison.Ordinal)) return;
        SqlConnection.ClearAllPools();
        await ExecAsync(_serverConnection,
            $"IF DB_ID(N'{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END");
    }

    private static async Task ExecAsync(string conn, string sql)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(SqlServerFixture.CollectionName)]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }
