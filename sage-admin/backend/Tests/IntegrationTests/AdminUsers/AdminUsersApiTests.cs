using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.AdminUsers;

[Collection(ApiFixture.CollectionName)]
public class AdminUsersApiTests
{
    private readonly ApiFixture _fx;
    public AdminUsersApiTests(ApiFixture fx) { _fx = fx; }

    private const string Base = "/api/admin/users";
    private const string Pwd = TestData.StrongPassword;
    private static string Uniq(string prefix) => (prefix + Guid.NewGuid().ToString("N"))[..16];

    private async Task<string> TokenAsync(TestUser u)
        => (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;

    private async Task<(TestUser user, string token)> UserWithAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return (u, await TokenAsync(u));
    }

    private async Task<(TestUser user, string token)> SuperAdminAsync()
    {
        var u = await TestData.CreateUserAsync(_fx.Db, isSuperAdmin: true);
        return (u, await TokenAsync(u));
    }

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    private async Task<Guid> GroupAsync(string token, params string[] permissions)
    {
        var res = await Call(HttpMethod.Post, "/api/admin/groups", token, new { code = Uniq("G"), name = Uniq("Group ") });
        res.Status.ShouldBe(201);
        var id = Guid.Parse(res.Data.GetProperty("id").GetString()!);
        if (permissions.Length > 0)
        {
            var ids = new List<long>();
            foreach (var p in permissions) ids.Add(await _fx.Db.ExecuteScalarAsync<long>("SELECT Id FROM dbo.Permissions WHERE Code = @c", new[] { TestDb.P("@c", p) }));
            (await Call(HttpMethod.Put, $"/api/admin/groups/{id}/permissions", token, new { permissionIds = ids })).Status.ShouldBe(200);
        }
        return id;
    }

    private static object NewUser(Guid? groupId = null, string? username = null, string? email = null, string password = Pwd)
    {
        username ??= Uniq("adm");
        return new { firstName = "New", lastName = "Admin", username, email = email ?? $"{username}@admin.example.com", mobileNumber = "+91 90000 00000", groupId, password, confirmPassword = password };
    }

    private async Task<(Guid id, string username)> CreateAsync(string token, Guid? groupId = null)
    {
        var username = Uniq("adm");
        var res = await Call(HttpMethod.Post, Base, token, NewUser(groupId, username));
        res.Status.ShouldBe(201, res.Root.ToString());
        return (Guid.Parse(res.Data.GetProperty("id").GetString()!), username);
    }

    // ---------------- 401 / 403 ----------------

    [Theory]
    [InlineData("GET", Base)]
    [InlineData("GET", Base + "/groups")]
    [InlineData("GET", Base + "/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", Base)]
    [InlineData("PUT", Base + "/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", Base + "/00000000-0000-0000-0000-000000000001/status")]
    [InlineData("POST", Base + "/00000000-0000-0000-0000-000000000001/reset-password")]
    public async Task EveryEndpoint_WithoutToken_Returns401(string method, string url)
        => (await Call(new HttpMethod(method), url, null, method == "GET" ? null : new { })).Status.ShouldBe(401);

    [Fact]
    public async Task EachEndpoint_NeedsItsOwnPermission()
    {
        var (_, admin) = await SuperAdminAsync();
        var (target, _) = await CreateAsync(admin);

        var (_, none) = await UserWithAsync("Group.View");
        (await Call(HttpMethod.Get, Base, none)).Status.ShouldBe(403);

        var (_, viewer) = await UserWithAsync("AdminUser.View");
        (await Call(HttpMethod.Get, Base, viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, $"{Base}/groups", viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Post, Base, viewer, NewUser())).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/{target}", viewer, new { firstName = "a", lastName = "b", email = "a@b.co" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/{target}/status", viewer, new { status = "Inactive" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, $"{Base}/{target}/reset-password", viewer, new { newPassword = Pwd, confirmPassword = Pwd })).Status.ShouldBe(403);

        var (_, creator) = await UserWithAsync("AdminUser.Add");
        (await Call(HttpMethod.Post, Base, creator, NewUser())).Status.ShouldBe(201);
    }

    // ---------------- create ----------------

    [Fact]
    public async Task Create_WithGroup_NewUserCanSignIn_MustChangePassword_AndGetsTheGroupsPermissions()
    {
        var (_, admin) = await SuperAdminAsync();
        var group = await GroupAsync(admin, "Product.View");
        var username = Uniq("adm");

        var res = await Call(HttpMethod.Post, Base, admin, NewUser(group, username));
        res.Status.ShouldBe(201);
        res.Data.GetProperty("groupId").GetString().ShouldBe(group.ToString());
        res.Data.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        res.Data.GetProperty("isSuperAdmin").GetBoolean().ShouldBeFalse();
        res.Data.TryGetProperty("passwordHash", out _).ShouldBeFalse();

        var login = await AuthHttp.LoginAsync(_fx.CreateClient(), username, Pwd);
        login.Status.ShouldBe(200);
        login.Data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        login.Data.GetProperty("user").GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ShouldBe(new[] { "Product.View" });
        (await Call(HttpMethod.Get, "/api/admin/products/catalog", login.AccessToken)).Status.ShouldBe(200);

        var audit = await _fx.Db.ExecuteScalarAsync<string>("SELECT NewValues FROM dbo.AuditLogs WHERE EntityType = 'AdminUser' AND Action = 'Create' AND EntityId = @id",
            new[] { TestDb.P("@id", res.Data.GetProperty("id").GetString()!) });
        audit!.ShouldNotContain(Pwd);
        audit.ShouldNotContain("v1.");
    }

    [Fact]
    public async Task Create_WithoutGroup_HasNoPermissions_AndBadInputIsRefused()
    {
        var (_, admin) = await SuperAdminAsync();
        var username = Uniq("adm");
        (await Call(HttpMethod.Post, Base, admin, NewUser(null, username))).Status.ShouldBe(201);
        var login = await AuthHttp.LoginAsync(_fx.CreateClient(), username, Pwd);
        login.Data.GetProperty("user").GetProperty("permissions").GetArrayLength().ShouldBe(0);

        var dupUser = await Call(HttpMethod.Post, Base, admin, NewUser(username: username, email: $"x{username}@admin.example.com"));
        dupUser.Status.ShouldBe(409);
        dupUser.FirstMessage.ShouldContain("username");
        (await Call(HttpMethod.Post, Base, admin, NewUser(email: $"{username}@admin.example.com"))).Status.ShouldBe(409);
        (await Call(HttpMethod.Post, Base, admin, NewUser(password: "weak"))).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, Base, admin, NewUser(username: "no spaces allowed"))).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, Base, admin, NewUser(groupId: Guid.NewGuid()))).Status.ShouldBe(400);

        var inactive = await GroupAsync(admin);
        var name = (await Call(HttpMethod.Get, $"/api/admin/groups/{inactive}", admin)).Data.GetProperty("name").GetString();
        (await Call(HttpMethod.Put, $"/api/admin/groups/{inactive}", admin, new { name, status = "Inactive" })).Status.ShouldBe(200);
        (await Call(HttpMethod.Post, Base, admin, NewUser(inactive))).Status.ShouldBe(400);
    }

    [Fact]
    public async Task Create_ExistingAdminRowsAreListed_IncludingSuperAdmins()
    {
        var (super, admin) = await SuperAdminAsync();
        var res = await Call(HttpMethod.Get, $"{Base}?search={super.Username}", admin);
        res.Status.ShouldBe(200);
        res.Data.GetProperty("items")[0].GetProperty("isSuperAdmin").GetBoolean().ShouldBeTrue();
    }

    // ---------------- update / group ----------------

    [Fact]
    public async Task Update_ChangingGroup_EndsOldSession_AndNewSessionHasNewAccess()
    {
        var (_, admin) = await SuperAdminAsync();
        var products = await GroupAsync(admin, "Product.View");
        var groups = await GroupAsync(admin, "Group.View");
        var (id, username) = await CreateAsync(admin, products);
        var before = (await AuthHttp.LoginAsync(_fx.CreateClient(), username, Pwd)).AccessToken;
        (await Call(HttpMethod.Get, "/api/admin/products/catalog", before)).Status.ShouldBe(200);

        var updated = await Call(HttpMethod.Put, $"{Base}/{id}", admin,
            new { firstName = "Moved", lastName = "Admin", email = $"{Uniq("mv")}@admin.example.com", groupId = groups });
        updated.Status.ShouldBe(200);
        updated.Data.GetProperty("groupId").GetString().ShouldBe(groups.ToString());

        (await Call(HttpMethod.Get, "/api/admin/products/catalog", before)).Status.ShouldBe(401);
        var after = (await AuthHttp.LoginAsync(_fx.CreateClient(), username, Pwd)).AccessToken;
        (await Call(HttpMethod.Get, "/api/admin/products/catalog", after)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, "/api/admin/groups", after)).Status.ShouldBe(200);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType = 'AdminUser' AND Action = 'AssignGroup' AND EntityId = @id",
            new[] { TestDb.P("@id", id.ToString()) })).ShouldBe(1);

        // Removing the group leaves no permissions.
        (await Call(HttpMethod.Put, $"{Base}/{id}", admin, new { firstName = "Moved", lastName = "Admin", email = $"{Uniq("mv")}@admin.example.com" })).Data
            .GetProperty("groupId").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task GroupWithMembers_CannotBeDeleted()
    {
        var (_, admin) = await SuperAdminAsync();
        var group = await GroupAsync(admin);
        await CreateAsync(admin, group);
        (await Call(HttpMethod.Delete, $"/api/admin/groups/{group}", admin)).Status.ShouldBe(409);
    }

    // ---------------- Super Admin and self rules ----------------

    [Fact]
    public async Task OnlyASuperAdmin_CanChangeASuperAdmin()
    {
        var (super, _) = await SuperAdminAsync();
        var (_, manager) = await UserWithAsync("AdminUser.View", "AdminUser.Edit", "AdminUser.ChangeStatus", "AdminUser.ResetPassword");

        (await Call(HttpMethod.Put, $"{Base}/{super.PublicId}", manager, new { firstName = "x", lastName = "y", email = super.Email })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/{super.PublicId}/status", manager, new { status = "Inactive" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, $"{Base}/{super.PublicId}/reset-password", manager, new { newPassword = Pwd, confirmPassword = Pwd })).Status.ShouldBe(403);

        var (_, otherSuper) = await SuperAdminAsync();
        (await Call(HttpMethod.Put, $"{Base}/{super.PublicId}", otherSuper, new { firstName = "Renamed", lastName = "Super", email = super.Email })).Status.ShouldBe(200);
    }

    [Fact]
    public async Task NobodyCanDeactivateOrResetThemselves()
    {
        var (me, token) = await SuperAdminAsync();
        var off = await Call(HttpMethod.Put, $"{Base}/{me.PublicId}/status", token, new { status = "Inactive" });
        off.Status.ShouldBe(400);
        off.FirstMessage.ShouldContain("your own");
        (await Call(HttpMethod.Post, $"{Base}/{me.PublicId}/reset-password", token, new { newPassword = Pwd, confirmPassword = Pwd })).Status.ShouldBe(400);
    }

    // ---------------- status / password ----------------

    [Fact]
    public async Task Deactivate_EndsSessionsAndBlocksLogin_ActivateClearsLockout()
    {
        var (_, admin) = await SuperAdminAsync();
        var (id, username) = await CreateAsync(admin, await GroupAsync(admin, "Product.View"));
        var login = await AuthHttp.LoginAsync(_fx.CreateClient(), username, Pwd);

        (await Call(HttpMethod.Put, $"{Base}/{id}/status", admin, new { status = "Inactive" })).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, "/api/admin/products/catalog", login.AccessToken)).Status.ShouldBe(401);
        (await AuthHttp.RefreshAsync(_fx.CreateClient(), login.RefreshCookie)).Status.ShouldBe(401);
        (await AuthHttp.LoginAsync(_fx.CreateClient(), username, Pwd)).Status.ShouldBe(401);

        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.AdminUsers SET LockoutEndUtc = DATEADD(MINUTE, 10, SYSUTCDATETIME()) WHERE PublicId = @p", new[] { TestDb.P("@p", id) });
        var on = await Call(HttpMethod.Put, $"{Base}/{id}/status", admin, new { status = "Active" });
        on.Data.GetProperty("isLockedOut").GetBoolean().ShouldBeFalse();
        (await AuthHttp.LoginAsync(_fx.CreateClient(), username, Pwd)).Status.ShouldBe(200);
        (await Call(HttpMethod.Put, $"{Base}/{id}/status", admin, new { status = "Locked" })).Status.ShouldBe(400);
    }

    [Fact]
    public async Task ResetPassword_OldPasswordStopsWorking_NewOneMustBeChanged()
    {
        var (_, admin) = await SuperAdminAsync();
        var (id, username) = await CreateAsync(admin);
        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.AdminUsers SET MustChangePassword = 0 WHERE PublicId = @p", new[] { TestDb.P("@p", id) });
        var login = await AuthHttp.LoginAsync(_fx.CreateClient(), username, Pwd);

        var res = await Call(HttpMethod.Post, $"{Base}/{id}/reset-password", admin, new { newPassword = "Temp#Passw0rd9", confirmPassword = "Temp#Passw0rd9" });
        res.Status.ShouldBe(200);
        (await AuthHttp.RefreshAsync(_fx.CreateClient(), login.RefreshCookie)).Status.ShouldBe(401);
        (await AuthHttp.LoginAsync(_fx.CreateClient(), username, Pwd)).Status.ShouldBe(401);
        var fresh = await AuthHttp.LoginAsync(_fx.CreateClient(), username, "Temp#Passw0rd9");
        fresh.Status.ShouldBe(200);
        fresh.Data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();

        (await Call(HttpMethod.Post, $"{Base}/{Guid.NewGuid()}/reset-password", admin, new { newPassword = Pwd, confirmPassword = Pwd })).Status.ShouldBe(404);
    }
}
