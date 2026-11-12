using Microsoft.Data.SqlClient;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models;

namespace SageUserApi.Tests.Infrastructure;

public static class TestDb
{
    public static IDatabaseFunctions Create(string connectionString, int timeoutSeconds = 30, int retries = 0)
    {
        var settings = new DatabaseSettings { ConnectionString = connectionString, CommandTimeoutSeconds = timeoutSeconds, MaxRetryCount = retries };
        return new DatabaseFunctions(new SqlConnectionFactory(settings), settings);
    }

    public static SqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

}
