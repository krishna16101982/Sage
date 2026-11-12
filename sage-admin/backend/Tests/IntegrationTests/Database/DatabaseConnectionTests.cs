using SageAdminApi.Tests.Infrastructure;
using SageCommonLib.Models;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Database;

[Collection(SqlServerFixture.CollectionName)]
public class DatabaseConnectionTests
{
    private readonly SqlServerFixture _fx;
    public DatabaseConnectionTests(SqlServerFixture fx) { _fx = fx; }

    [Fact]
    public async Task Connection_Success_PingReturnsTrue()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        (await db.PingAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task Connection_Failure_PingReturnsFalse()
    {
        var db = TestDb.Create("Server=127.0.0.1,1;Database=x;User Id=nobody;Password=nope;Connect Timeout=2;Encrypt=false");
        (await db.PingAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Connection_Failure_QueryThrowsDatabaseExceptionWithoutLeakingDetails()
    {
        var db = TestDb.Create("Server=127.0.0.1,1;Database=x;User Id=nobody;Password=nope;Connect Timeout=2;Encrypt=false");
        var ex = await Should.ThrowAsync<DatabaseException>(() => db.ExecuteScalarAsync<int>("SELECT 1"));
        ex.Message.ShouldNotContain("127.0.0.1");
    }

    [Fact]
    public async Task Connection_MissingConnectionString_Throws()
    {
        var db = TestDb.Create("");
        await Should.ThrowAsync<DatabaseException>(() => db.ExecuteScalarAsync<int>("SELECT 1"));
    }
}
