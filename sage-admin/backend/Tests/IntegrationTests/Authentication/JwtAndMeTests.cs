using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Authentication;

[Collection(ApiFixture.CollectionName)]
public class JwtAndMeTests
{
    private readonly ApiFixture _fx;
    public JwtAndMeTests(ApiFixture fx) { _fx = fx; }

    [Fact]
    public async Task Me_WithoutToken_Returns401WithEnvelope()
    {
        var res = await AuthHttp.MeAsync(_fx.CreateClient(), null);
        res.Status.ShouldBe(401);
        res.Root.GetProperty("status").GetBoolean().ShouldBeFalse();
        res.Root.GetProperty("messageType").GetString().ShouldBe("TOKEN_ERROR");
    }

    [Fact]
    public async Task ExpiredJwt_Returns401()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        var token = AuthHttp.ForgeToken(user.PublicId, 1, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));
        (await AuthHttp.MeAsync(_fx.CreateClient(), token)).Status.ShouldBe(401);
    }

    [Fact]
    public async Task JwtSignedWithAnotherKey_Returns401()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        var token = AuthHttp.ForgeToken(user.PublicId, 1, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10), key: "some-other-signing-key-0123456789-zzzzzz");
        (await AuthHttp.MeAsync(_fx.CreateClient(), token)).Status.ShouldBe(401);
    }

    [Fact]
    public async Task Garbage_Bearer_Returns401()
        => (await AuthHttp.MeAsync(_fx.CreateClient(), "not.a.jwt")).Status.ShouldBe(401);

    [Fact]
    public async Task ValidJwtForUnknownUser_Returns401()
    {
        var token = AuthHttp.ForgeToken(Guid.NewGuid(), 1, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10));
        (await AuthHttp.MeAsync(_fx.CreateClient(), token)).Status.ShouldBe(401);
    }

    [Fact]
    public async Task ValidJwt_ForDeactivatedUser_IsRejectedImmediately()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        var login = await AuthHttp.LoginAsync(c, user.Username, user.Password);
        (await AuthHttp.MeAsync(c, login.AccessToken)).Status.ShouldBe(200);

        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.AdminUsers SET Status='Inactive' WHERE Id=@i", new[] { TestDb.P("@i", user.Id) });

        (await AuthHttp.MeAsync(c, login.AccessToken)).Status.ShouldBe(401);
    }

    [Fact]
    public async Task PermissionVersionBump_InvalidatesOlderAccessTokens()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        var login = await AuthHttp.LoginAsync(c, user.Username, user.Password);

        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.AdminUsers SET PermissionVersion = PermissionVersion + 1 WHERE Id=@i", new[] { TestDb.P("@i", user.Id) });

        (await AuthHttp.MeAsync(c, login.AccessToken)).Status.ShouldBe(401);
        var refreshed = await AuthHttp.RefreshAsync(c, login.RefreshCookie);   // a refresh yields a token with the new version
        (await AuthHttp.MeAsync(c, refreshed.AccessToken)).Status.ShouldBe(200);
    }

    [Fact]
    public async Task Me_SuperAdmin_GetsAllActivePermissionsFromSql()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db, isSuperAdmin: true);
        var login = await AuthHttp.LoginAsync(c, user.Username, user.Password);
        var me = await AuthHttp.MeAsync(c, login.AccessToken);

        me.Status.ShouldBe(200);
        me.Data.GetProperty("isSuperAdmin").GetBoolean().ShouldBeTrue();
        var perms = me.Data.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ToList();
        var total = await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Permissions p JOIN dbo.Modules m ON m.Id=p.ModuleId WHERE p.IsActive=1 AND m.IsActive=1");
        perms.Count.ShouldBe(total);
        perms.ShouldContain("Customer.Delete");
        perms.ShouldContain("AdminUser.Add");
    }

    [Fact]
    public async Task Me_GroupUser_GetsOnlyTheirGroupPermissions()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        await TestData.AssignGroupAsync(_fx.Db, user, "Customer.View", "Customer.Add", "Customer.Edit", "Invoice.View");
        var login = await AuthHttp.LoginAsync(c, user.Username, user.Password);
        var me = await AuthHttp.MeAsync(c, login.AccessToken);

        var perms = me.Data.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).OrderBy(x => x).ToList();
        perms.ShouldBe(new[] { "Customer.Add", "Customer.Edit", "Customer.View", "Invoice.View" });
        perms.ShouldNotContain("Customer.Delete");
        me.Data.GetProperty("modules").EnumerateArray().Select(m => m.GetProperty("code").GetString()).OrderBy(x => x)
            .ShouldBe(new[] { "Customer", "Invoice" });
    }

    [Fact]
    public async Task Me_UserWithoutGroup_HasNoPermissions()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        var login = await AuthHttp.LoginAsync(c, user.Username, user.Password);
        var me = await AuthHttp.MeAsync(c, login.AccessToken);
        me.Data.GetProperty("permissions").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Status_RemainsAnonymous()
        => (await AuthHttp.SendAsync(_fx.CreateClient(), HttpMethod.Get, "/api/admin/status")).Status.ShouldBe(200);

    // ---------------- sidebar menu (dbo.Modules.MenuPath) ----------------

    [Fact]
    public async Task Me_Menu_SuperAdmin_SeesEveryLinkedScreenInSortOrder()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db, isSuperAdmin: true);
        var me = await AuthHttp.MeAsync(c, (await AuthHttp.LoginAsync(c, user.Username, user.Password)).AccessToken);

        var menu = me.Data.GetProperty("menu").EnumerateArray().ToList();
        var seeded = new[] { "/dashboard", "/users", "/groups", "/permissions", "/audit-logs", "/settings", "/products", "/sales-users" };
        menu.Select(m => m.GetProperty("path").GetString()).Where(seeded.Contains).ShouldBe(seeded);
        menu.First().GetProperty("category").GetString().ShouldBe("Administration");
        menu.Single(m => m.GetProperty("code").GetString() == "Product").GetProperty("name").GetString().ShouldBe("Product Catalog");
        menu.ShouldNotContain(m => m.GetProperty("code").GetString() == "Customer"); // no screen (no link): not in the menu
    }

    [Fact]
    public async Task Me_Menu_GroupUser_SeesOnlyLinkedModulesTheyCanView()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        await TestData.AssignGroupAsync(_fx.Db, user, "Product.View", "Product.Add", "Customer.View", "SalesUser.Add");
        var me = await AuthHttp.MeAsync(c, (await AuthHttp.LoginAsync(c, user.Username, user.Password)).AccessToken);

        // Product.View -> Products. Customer has no link; SalesUser.Add without SalesUser.View does not show the menu item.
        me.Data.GetProperty("menu").EnumerateArray().Select(m => m.GetProperty("path").GetString()).ShouldBe(new[] { "/products" });
    }
}
