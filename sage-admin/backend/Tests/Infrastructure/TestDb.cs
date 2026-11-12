using Microsoft.Data.SqlClient;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models;

namespace SageAdminApi.Tests.Infrastructure;

public static class TestDb
{
    public static IDatabaseFunctions Create(string connectionString, int timeoutSeconds = 30, int retries = 0)
    {
        var settings = new DatabaseSettings { ConnectionString = connectionString, CommandTimeoutSeconds = timeoutSeconds, MaxRetryCount = retries };
        return new DatabaseFunctions(new SqlConnectionFactory(settings), settings);
    }

    public static SqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    public static Task<int> InsertUserAsync(IDatabaseFunctions db, string username, string email, string status = "Active")
        => db.ExecuteNonQueryAsync(
            "INSERT dbo.AdminUsers (FirstName, LastName, Username, Email, PasswordHash, Status) VALUES (N'T', N'U', @u, @e, N'x', @s)",
            new[] { P("@u", username), P("@e", email), P("@s", status) });
}
