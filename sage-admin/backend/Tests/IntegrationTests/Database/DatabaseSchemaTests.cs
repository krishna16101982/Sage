using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Database;

[Collection(SqlServerFixture.CollectionName)]
public class DatabaseSchemaTests
{
    private readonly SqlServerFixture _fx;
    public DatabaseSchemaTests(SqlServerFixture fx) { _fx = fx; }

    private static readonly string[] ExpectedTables =
    {
        "SchemaVersions", "AdminUsers", "Modules", "Permissions", "PermissionGroups", "GroupPermissions",
        "AdminUserGroups", "RefreshTokens", "PasswordResetTokens", "LoginHistories", "AuditLogs", "Settings",
        "ProductCategories", "Products", "ProductPlans"
    };

    [Fact]
    public async Task Schema_AllExpectedTablesExist()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var tables = await db.QueryAsync("SELECT name FROM sys.tables", r => r.GetString(0));
        foreach (var t in ExpectedTables) tables.ShouldContain(t);
    }

    [Theory]
    [InlineData("AdminUsers", "PermissionVersion")]
    [InlineData("AdminUsers", "PasswordHash")]
    [InlineData("AdminUsers", "LockoutEndUtc")]
    [InlineData("Permissions", "Code")]
    [InlineData("RefreshTokens", "TokenHash")]
    [InlineData("AuditLogs", "NewValues")]
    public async Task Schema_ExpectedColumnsExist(string table, string column)
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var count = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(@t) AND name = @c",
            new[] { TestDb.P("@t", "dbo." + table), TestDb.P("@c", column) });
        count.ShouldBe(1);
    }

    [Fact]
    public async Task Schema_EveryTableHasPrimaryKey_AndConstraintsAreNamed()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var withoutPk = await db.QueryAsync(
            "SELECT t.name FROM sys.tables t WHERE NOT EXISTS (SELECT 1 FROM sys.key_constraints k WHERE k.parent_object_id = t.object_id AND k.type = 'PK')",
            r => r.GetString(0));
        withoutPk.ShouldBeEmpty();

        var systemNamed = await db.QueryAsync(
            "SELECT name FROM sys.objects WHERE type IN ('D','C','F','UQ','PK') AND is_ms_shipped = 0 AND name LIKE '%[_][_]%[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]%'",
            r => r.GetString(0));
        systemNamed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Seed_ModulesAndPermissionsLoaded_AndViewAndFunctionExist()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Permissions WHERE Code = 'Employee.View'")).ShouldBe(1);
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Permissions WHERE Code LIKE 'FinancialReports.%'")).ShouldBe(2);
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.views WHERE name = 'vw_AdminUserEffectivePermissions'")).ShouldBe(1);
        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.objects WHERE name = 'fn_UserHasPermission' AND type = 'FN'")).ShouldBe(1);
    }

    [Fact]
    public async Task StoredProcedures_EveryProcedureTheApiCallsExists()
    {
        var db = TestDb.Create(_fx.ConnectionString);
        var names = typeof(SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures)
            .GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();
        names.ShouldNotBeEmpty();
        foreach (var name in names)
            (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.procedures WHERE object_id = OBJECT_ID(@n)", new[] { TestDb.P("@n", name) }))
                .ShouldBe(1, $"missing stored procedure {name}");
    }

    [Fact]
    public async Task Scripts_AreIdempotent_RerunningAllScriptsSucceedsWithoutDuplicates()
    {
        var before = TestDb.Create(_fx.ConnectionString);
        var permsBefore = await before.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Permissions");
        await SqlScriptRunner.ApplyAllAsync(_fx.ConnectionString);
        (await before.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Permissions")).ShouldBe(permsBefore);
    }

    [Fact]
    public void Scripts_DdlFilesAreNumberedSequentiallyWithoutGaps()
    {
        var names = SqlScriptRunner.ScriptFiles("DDL").Select(Path.GetFileName).ToList();
        for (var i = 0; i < names.Count; i++)
            names[i]!.ShouldStartWith((i + 1).ToString("000") + "_");
    }
}
