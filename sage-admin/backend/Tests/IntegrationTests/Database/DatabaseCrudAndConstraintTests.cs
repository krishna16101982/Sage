using Microsoft.Data.SqlClient;
using SageAdminApi.Tests.Infrastructure;
using SageCommonLib.Models;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Database;

[Collection(SqlServerFixture.CollectionName)]
public class DatabaseCrudAndConstraintTests
{
    private readonly SqlServerFixture _fx;
    public DatabaseCrudAndConstraintTests(SqlServerFixture fx) { _fx = fx; }

    private static string U() => "u" + Guid.NewGuid().ToString("N")[..12];

    [Fact]
    public async Task Crud_InsertSelectUpdateSoftDelete()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var name = U();
        (await TestDb.InsertUserAsync(db, name, name + "@t.io")).ShouldBe(1);

        var status = await db.ExecuteScalarAsync<string>("SELECT Status FROM dbo.AdminUsers WHERE Username=@u", new[] { TestDb.P("@u", name) });
        status.ShouldBe("Active");

        await db.ExecuteNonQueryAsync("UPDATE dbo.AdminUsers SET Status='Inactive' WHERE Username=@u", new[] { TestDb.P("@u", name) });
        (await db.ExecuteScalarAsync<string>("SELECT Status FROM dbo.AdminUsers WHERE Username=@u", new[] { TestDb.P("@u", name) })).ShouldBe("Inactive");

        await db.ExecuteNonQueryAsync("UPDATE dbo.AdminUsers SET IsDeleted=1 WHERE Username=@u", new[] { TestDb.P("@u", name) });
        // soft-deleted username can be reused
        (await TestDb.InsertUserAsync(db, name, name + "@t.io")).ShouldBe(1);
    }

    [Fact]
    public async Task Constraint_DuplicateUsername_ThrowsConflict()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var name = U();
        await TestDb.InsertUserAsync(db, name, name + "@a.io");
        await Should.ThrowAsync<ConflictException>(() => TestDb.InsertUserAsync(db, name, name + "@b.io"));
    }

    [Fact]
    public async Task Constraint_DuplicateEmail_ThrowsConflict()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var email = U() + "@dup.io";
        await TestDb.InsertUserAsync(db, U(), email);
        await Should.ThrowAsync<ConflictException>(() => TestDb.InsertUserAsync(db, U(), email));
    }

    [Fact]
    public async Task Constraint_InvalidStatus_IsRejectedByCheckConstraint()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        await Should.ThrowAsync<ConflictException>(() => TestDb.InsertUserAsync(db, U(), U() + "@t.io", status: "Bogus"));
    }

    [Fact]
    public async Task Constraint_DuplicateGroupCode_ThrowsConflict()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var code = "G" + Guid.NewGuid().ToString("N")[..10];
        const string sql = "INSERT dbo.PermissionGroups (Name, Code) VALUES (@n, @c)";
        await db.ExecuteNonQueryAsync(sql, new[] { TestDb.P("@n", code + "a"), TestDb.P("@c", code) });
        await Should.ThrowAsync<ConflictException>(() => db.ExecuteNonQueryAsync(sql, new[] { TestDb.P("@n", code + "b"), TestDb.P("@c", code) }));
    }

    [Fact]
    public async Task Constraint_ForeignKey_GroupAssignedToUserCannotBeDeleted()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var name = U();
        var code = "G" + Guid.NewGuid().ToString("N")[..10];
        await TestDb.InsertUserAsync(db, name, name + "@t.io");
        await db.ExecuteNonQueryAsync("INSERT dbo.PermissionGroups (Name, Code) VALUES (@c, @c)", new[] { TestDb.P("@c", code) });
        await db.ExecuteNonQueryAsync(
            "INSERT dbo.AdminUserGroups (UserId, GroupId) SELECT u.Id, g.Id FROM dbo.AdminUsers u, dbo.PermissionGroups g WHERE u.Username=@u AND g.Code=@c",
            new[] { TestDb.P("@u", name), TestDb.P("@c", code) });
        await Should.ThrowAsync<ConflictException>(() =>
            db.ExecuteNonQueryAsync("DELETE FROM dbo.PermissionGroups WHERE Code=@c", new[] { TestDb.P("@c", code) }));
    }

    [Fact]
    public async Task Constraint_OnlyOnePrimaryGroupPerUser()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var name = U();
        var c1 = "G" + Guid.NewGuid().ToString("N")[..10];
        var c2 = "G" + Guid.NewGuid().ToString("N")[..10];
        await TestDb.InsertUserAsync(db, name, name + "@t.io");
        foreach (var c in new[] { c1, c2 })
            await db.ExecuteNonQueryAsync("INSERT dbo.PermissionGroups (Name, Code) VALUES (@c, @c)", new[] { TestDb.P("@c", c) });
        const string assign = "INSERT dbo.AdminUserGroups (UserId, GroupId, IsPrimary) SELECT u.Id, g.Id, @p FROM dbo.AdminUsers u, dbo.PermissionGroups g WHERE u.Username=@u AND g.Code=@c";
        await db.ExecuteNonQueryAsync(assign, new[] { TestDb.P("@u", name), TestDb.P("@c", c1), TestDb.P("@p", true) });
        await db.ExecuteNonQueryAsync(assign, new[] { TestDb.P("@u", name), TestDb.P("@c", c2), TestDb.P("@p", false) }); // secondary OK
        await Should.ThrowAsync<ConflictException>(() =>
            db.ExecuteNonQueryAsync("UPDATE dbo.AdminUserGroups SET IsPrimary = 1 WHERE GroupId = (SELECT Id FROM dbo.PermissionGroups WHERE Code=@c)", new[] { TestDb.P("@c", c2) }));
    }

    [Fact]
    public async Task Transaction_Rollback_OnException_LeavesNoRows()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var name = U();
        await Should.ThrowAsync<InvalidOperationException>(() => db.ExecuteInTransactionAsync<int>(async (conn, tx) =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT dbo.AdminUsers (FirstName, LastName, Username, Email, PasswordHash) VALUES (N'a', N'b', @u, @e, N'x')";
            cmd.Parameters.AddWithValue("@u", name);
            cmd.Parameters.AddWithValue("@e", name + "@t.io");
            await cmd.ExecuteNonQueryAsync();
            throw new InvalidOperationException("boom");
        }));
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AdminUsers WHERE Username=@u", new[] { TestDb.P("@u", name) })).ShouldBe(0);
    }

    [Fact]
    public async Task Transaction_Commit_PersistsRows()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var name = U();
        await db.ExecuteInTransactionAsync(async (conn, tx) =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT dbo.AdminUsers (FirstName, LastName, Username, Email, PasswordHash) VALUES (N'a', N'b', @u, @e, N'x')";
            cmd.Parameters.AddWithValue("@u", name);
            cmd.Parameters.AddWithValue("@e", name + "@t.io");
            return await cmd.ExecuteNonQueryAsync();
        });
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AdminUsers WHERE Username=@u", new[] { TestDb.P("@u", name) })).ShouldBe(1);
    }

    [Fact]
    public async Task Parameters_PreventSqlInjection()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var evil = "x'; DROP TABLE dbo.AdminUsers; --";
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AdminUsers WHERE Username=@u", new[] { TestDb.P("@u", evil) })).ShouldBe(0);
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name='AdminUsers'")).ShouldBe(1);
    }

    [Fact]
    public async Task Permissions_EffectiveViewAndFunction_ReflectGroupAssignments()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var name = U();
        var code = "G" + Guid.NewGuid().ToString("N")[..10];
        await TestDb.InsertUserAsync(db, name, name + "@t.io");
        await db.ExecuteNonQueryAsync("INSERT dbo.PermissionGroups (Name, Code) VALUES (@c, @c)", new[] { TestDb.P("@c", code) });
        await db.ExecuteNonQueryAsync("INSERT dbo.GroupPermissions (GroupId, PermissionId) SELECT g.Id, p.Id FROM dbo.PermissionGroups g, dbo.Permissions p WHERE g.Code=@c AND p.Code IN ('Employee.View','Employee.Add')", new[] { TestDb.P("@c", code) });
        await db.ExecuteNonQueryAsync("INSERT dbo.AdminUserGroups (UserId, GroupId) SELECT u.Id, g.Id FROM dbo.AdminUsers u, dbo.PermissionGroups g WHERE u.Username=@u AND g.Code=@c", new[] { TestDb.P("@u", name), TestDb.P("@c", code) });

        var perms = await db.QueryAsync("SELECT PermissionCode FROM dbo.vw_AdminUserEffectivePermissions v JOIN dbo.AdminUsers u ON u.Id=v.UserId WHERE u.Username=@u",
            r => r.GetString(0), new[] { TestDb.P("@u", name) });
        perms.OrderBy(x => x).ShouldBe(new[] { "Employee.Add", "Employee.View" });

        var has = await db.ExecuteScalarAsync<bool>("SELECT dbo.fn_UserHasPermission((SELECT Id FROM dbo.AdminUsers WHERE Username=@u), 'Employee.Delete')", new[] { TestDb.P("@u", name) });
        has.ShouldBeFalse();

        // deactivating the group removes access
        await db.ExecuteNonQueryAsync("UPDATE dbo.PermissionGroups SET Status='Inactive' WHERE Code=@c", new[] { TestDb.P("@c", code) });
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.vw_AdminUserEffectivePermissions v JOIN dbo.AdminUsers u ON u.Id=v.UserId WHERE u.Username=@u", new[] { TestDb.P("@u", name) })).ShouldBe(0);
    }
}
