using System.Text;
using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Database;

/// <summary>ExecuteInsert/Update/Delete/CheckExistsAndInsert: the ixcheck-style write wrappers (parameterized, never throw for expected DB errors).</summary>
[Collection(SqlServerFixture.CollectionName)]
public class DatabaseWriteWrapperTests
{
    private readonly SqlServerFixture _fx;
    public DatabaseWriteWrapperTests(SqlServerFixture fx) { _fx = fx; }

    private static string Code() => "W" + Guid.NewGuid().ToString("N")[..10];

    private static StringBuilder InsertGroup()
    {
        var q = new StringBuilder();
        q.Append(" INSERT dbo.PermissionGroups (Name, Code, Description) ");
        q.Append(" VALUES (@name, @code, @description) ");
        return q;
    }

    private static Microsoft.Data.SqlClient.SqlParameter[] Params(string code, string? description = null)
        => new[] { TestDb.P("@name", code + " name"), TestDb.P("@code", code), TestDb.P("@description", description) };

    [Fact]
    public async Task ExecuteInsert_ReturnsTheNewIdentity_AndTheRowExists()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var code = Code();
        var (fr, id) = await db.ExecuteInsertAsync(InsertGroup().ToString(), Params(code), "T:Insert", "created");

        fr.Status.ShouldBeTrue();
        fr.Message.ShouldBe(new List<string> { "created" });
        id.ShouldBeGreaterThan(0);
        (await db.ExecuteScalarAsync<string>("SELECT Code FROM dbo.PermissionGroups WHERE Id=@i", new[] { TestDb.P("@i", id) })).ShouldBe(code);
    }

    [Fact]
    public async Task ExecuteInsert_DuplicateKey_ReturnsFailedFunctionReturn_NotAnException()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var code = Code();
        await db.ExecuteInsertAsync(InsertGroup().ToString(), Params(code), "T:Insert");

        var (fr, id) = await db.ExecuteInsertAsync(InsertGroup().ToString(), Params(code), "T:Insert");

        fr.Status.ShouldBeFalse();
        fr.HttpStatusCode.ShouldBe("409");
        fr.MessageType.ShouldBe("CONFLICT");
        fr.MethodName.ShouldBe("T:Insert");
        id.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteInsert_ValuesAreParameters_SoInjectionIsStoredAsPlainText()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var code = Code();
        var evil = "'); DROP TABLE dbo.PermissionGroups; --";
        var (fr, id) = await db.ExecuteInsertAsync(InsertGroup().ToString(), Params(code, evil), "T:Insert");

        fr.Status.ShouldBeTrue();
        (await db.ExecuteScalarAsync<string>("SELECT Description FROM dbo.PermissionGroups WHERE Id=@i", new[] { TestDb.P("@i", id) })).ShouldBe(evil);
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name='PermissionGroups'")).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteCheckExistsAndInsert_BlocksDuplicates_WithTheCallersMessage_AndReusesTheSameParameters()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var code = Code();
        var p = Params(code);
        const string check = "SELECT COUNT(*) FROM dbo.PermissionGroups WHERE Code = @code";

        var first = await db.ExecuteCheckExistsAndInsertAsync(check, InsertGroup().ToString(), p, "T:CheckInsert", "Group exists", "ok");
        var second = await db.ExecuteCheckExistsAndInsertAsync(check, InsertGroup().ToString(), p, "T:CheckInsert", "Group exists", "ok");

        first.functionReturn.Status.ShouldBeTrue();
        first.retValue.ShouldBeGreaterThan(0);
        second.functionReturn.Status.ShouldBeFalse();
        second.functionReturn.HttpStatusCode.ShouldBe("409");
        second.functionReturn.Message.ShouldBe(new List<string> { "Group exists" });
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PermissionGroups WHERE Code=@c", new[] { TestDb.P("@c", code) })).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteCheckExistsAndInsert_ConcurrentDuplicates_ExactlyOneWins()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var code = Code();
        const string check = "SELECT COUNT(*) FROM dbo.PermissionGroups WHERE Code = @code";
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            db.ExecuteCheckExistsAndInsertAsync(check, InsertGroup().ToString(), Params(code), "T:Race", "Group exists")));

        results.Count(r => r.functionReturn.Status).ShouldBe(1);
        results.Where(r => !r.functionReturn.Status).ShouldAllBe(r => r.functionReturn.HttpStatusCode == "409");
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PermissionGroups WHERE Code=@c", new[] { TestDb.P("@c", code) })).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteUpdate_ReturnsRowsAffected_AndZeroWhenNothingMatches()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var code = Code();
        await db.ExecuteInsertAsync(InsertGroup().ToString(), Params(code), "T:Insert");

        var q = new StringBuilder();
        q.Append(" UPDATE dbo.PermissionGroups SET Description = @d WHERE Code = @c ");
        var hit = await db.ExecuteUpdateAsync(q.ToString(), new[] { TestDb.P("@d", "changed"), TestDb.P("@c", code) }, "T:Update", "updated");
        var miss = await db.ExecuteUpdateAsync(q.ToString(), new[] { TestDb.P("@d", "changed"), TestDb.P("@c", "does-not-exist") }, "T:Update");

        hit.functionReturn.Status.ShouldBeTrue();
        hit.retValue.ShouldBe(1);
        miss.functionReturn.Status.ShouldBeTrue();
        miss.retValue.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteUpdate_ConstraintViolation_ReturnsFailure()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var (a, b) = (Code(), Code());
        await db.ExecuteInsertAsync(InsertGroup().ToString(), Params(a), "T:Insert");
        await db.ExecuteInsertAsync(InsertGroup().ToString(), Params(b), "T:Insert");

        var (fr, rows) = await db.ExecuteUpdateAsync("UPDATE dbo.PermissionGroups SET Code = @a WHERE Code = @b", new[] { TestDb.P("@a", a), TestDb.P("@b", b) }, "T:Update");

        fr.Status.ShouldBeFalse();
        fr.HttpStatusCode.ShouldBe("409");
        rows.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteUpdate_MultiStatementTransactionBatch_RollsBackEverythingOnFailure()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var (a, b) = (Code(), Code());
        await db.ExecuteInsertAsync(InsertGroup().ToString(), Params(a), "T:Insert");
        await db.ExecuteInsertAsync(InsertGroup().ToString(), Params(b), "T:Insert");

        var q = new StringBuilder();
        q.Append(" SET XACT_ABORT ON; BEGIN TRAN; ");
        q.Append(" UPDATE dbo.PermissionGroups SET Description = 'first' WHERE Code = @b; ");
        q.Append(" UPDATE dbo.PermissionGroups SET Code = @a WHERE Code = @b; ");   // violates the unique constraint
        q.Append(" COMMIT; ");
        var (fr, _) = await db.ExecuteUpdateAsync(q.ToString(), new[] { TestDb.P("@a", a), TestDb.P("@b", b) }, "T:Batch");

        fr.Status.ShouldBeFalse();
        (await db.ExecuteScalarAsync<string?>("SELECT Description FROM dbo.PermissionGroups WHERE Code=@b", new[] { TestDb.P("@b", b) })).ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteDelete_ReturnsRowsAffected_AndForeignKeyConflictIsAFailureNotAnException()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var code = Code();
        var user = "u" + Guid.NewGuid().ToString("N")[..12];
        var (_, groupId) = await db.ExecuteInsertAsync(InsertGroup().ToString(), Params(code), "T:Insert");
        await TestDb.InsertUserAsync(db, user, user + "@t.io");
        await db.ExecuteNonQueryAsync("INSERT dbo.AdminUserGroups (UserId, GroupId) SELECT Id, @g FROM dbo.AdminUsers WHERE Username=@u", new[] { TestDb.P("@g", groupId), TestDb.P("@u", user) });

        var blocked = await db.ExecuteDeleteAsync("DELETE FROM dbo.PermissionGroups WHERE Id = @id", new[] { TestDb.P("@id", groupId) }, "T:Delete");
        blocked.functionReturn.Status.ShouldBeFalse();
        blocked.functionReturn.HttpStatusCode.ShouldBe("409");

        await db.ExecuteDeleteAsync("DELETE FROM dbo.AdminUserGroups WHERE GroupId = @id", new[] { TestDb.P("@id", groupId) }, "T:Delete");
        var ok = await db.ExecuteDeleteAsync("DELETE FROM dbo.PermissionGroups WHERE Id = @id", new[] { TestDb.P("@id", groupId) }, "T:Delete", "deleted");
        ok.functionReturn.Status.ShouldBeTrue();
        ok.retValue.ShouldBe(1);
        ok.functionReturn.Message.ShouldBe(new List<string> { "deleted" });
    }
}
