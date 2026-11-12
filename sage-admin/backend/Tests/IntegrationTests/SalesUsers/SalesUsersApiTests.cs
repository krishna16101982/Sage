using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.SalesUsers;

[Collection(ApiFixture.CollectionName)]
public class SalesUsersApiTests
{
    private readonly ApiFixture _fx;
    public SalesUsersApiTests(ApiFixture fx) { _fx = fx; }

    private const string Base = "/api/admin/sales-users";
    private const string Pwd = TestData.StrongPassword;
    private static string Uniq(string prefix) => (prefix + Guid.NewGuid().ToString("N"))[..16];

    private async Task<string> TokenAsync(TestUser u)
        => (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;

    private async Task<string> UserTokenAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return await TokenAsync(u);
    }

    private async Task<string> AdminTokenAsync() => await TokenAsync(await TestData.CreateUserAsync(_fx.Db, isSuperAdmin: true));

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    private static object NewUser(string role, Guid? managerId = null, string? username = null, string? email = null, string password = Pwd, string? confirm = null)
    {
        username ??= Uniq("su");
        return new
        {
            firstName = "Test", lastName = role, username, email = email ?? $"{username}@sales.example.com", mobileNumber = "+91 98765 43210",
            role, managerId, password, confirmPassword = confirm ?? password
        };
    }

    private async Task<Guid> CreateAsync(string token, string role, Guid? managerId = null)
    {
        var res = await Call(HttpMethod.Post, Base, token, NewUser(role, managerId));
        res.Status.ShouldBe(201, res.Root.ToString());
        return Guid.Parse(res.Data.GetProperty("id").GetString()!);
    }

    private async Task<int> PermissionVersionAsync(Guid id)
        => await _fx.Db.ExecuteScalarAsync<int>("SELECT PermissionVersion FROM dbo.SalesUsers WHERE PublicId = @p", new[] { TestDb.P("@p", id) });

    // ---------------- 401 / 403 ----------------

    [Theory]
    [InlineData("GET", Base)]
    [InlineData("GET", Base + "/managers")]
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
        var admin = await AdminTokenAsync();
        var manager = await CreateAsync(admin, "SalesManager");
        var none = await UserTokenAsync("Group.View", "Product.View");
        (await Call(HttpMethod.Get, Base, none)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, $"{Base}/managers", none)).Status.ShouldBe(403);

        var viewer = await UserTokenAsync("SalesUser.View");
        (await Call(HttpMethod.Get, Base, viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, $"{Base}/{manager}", viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Post, Base, viewer, NewUser("SalesManager"))).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/{manager}", viewer, new { firstName = "x", lastName = "y", email = "a@b.co", role = "SalesManager" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/{manager}/status", viewer, new { status = "Inactive" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, $"{Base}/{manager}/reset-password", viewer, new { newPassword = Pwd, confirmPassword = Pwd })).Status.ShouldBe(403);

        // Editing does not include status changes or password resets.
        var editor = await UserTokenAsync("SalesUser.Edit");
        (await Call(HttpMethod.Put, $"{Base}/{manager}/status", editor, new { status = "Inactive" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, $"{Base}/{manager}/reset-password", editor, new { newPassword = Pwd, confirmPassword = Pwd })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/{manager}/status", await UserTokenAsync("SalesUser.ChangeStatus"), new { status = "Active" })).Status.ShouldBe(200);
        (await Call(HttpMethod.Post, $"{Base}/{manager}/reset-password", await UserTokenAsync("SalesUser.ResetPassword"), new { newPassword = Pwd, confirmPassword = Pwd })).Status.ShouldBe(200);
    }

    // ---------------- create ----------------

    [Fact]
    public async Task Create_ManagerAndSalesPerson_WithTemporaryPassword_NoPasswordDataReturnedOrAudited()
    {
        var admin = await AdminTokenAsync();
        var manager = await CreateAsync(admin, "SalesManager");
        var res = await Call(HttpMethod.Post, Base, admin, NewUser("SalesPerson", manager));

        res.Status.ShouldBe(201);
        res.Data.GetProperty("role").GetString().ShouldBe("SalesPerson");
        res.Data.GetProperty("status").GetString().ShouldBe("Active");
        res.Data.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        res.Data.GetProperty("managerId").GetString().ShouldBe(manager.ToString());
        res.Data.GetProperty("managerName").GetString().ShouldBe("Test SalesManager");
        res.Data.TryGetProperty("passwordHash", out _).ShouldBeFalse();
        res.Data.TryGetProperty("password", out _).ShouldBeFalse();

        var id = res.Data.GetProperty("id").GetString()!;
        var hash = await _fx.Db.ExecuteScalarAsync<string>("SELECT PasswordHash FROM dbo.SalesUsers WHERE PublicId = @p", new[] { TestDb.P("@p", Guid.Parse(id)) });
        hash!.ShouldStartWith("v1.");
        var audit = await _fx.Db.ExecuteScalarAsync<string>("SELECT NewValues FROM dbo.AuditLogs WHERE EntityType = 'SalesUser' AND Action = 'Create' AND EntityId = @id",
            new[] { TestDb.P("@id", id) });
        audit!.ShouldNotContain("v1.");
        audit.ShouldNotContain(Pwd);

        var team = await Call(HttpMethod.Get, $"{Base}/{manager}", admin);
        team.Data.GetProperty("teamSize").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Create_RoleAndManagerRules_Return400()
    {
        var admin = await AdminTokenAsync();
        var manager = await CreateAsync(admin, "SalesManager");
        var person = await CreateAsync(admin, "SalesPerson", manager);
        var inactiveManager = await CreateAsync(admin, "SalesManager");
        (await Call(HttpMethod.Put, $"{Base}/{inactiveManager}/status", admin, new { status = "Inactive" })).Status.ShouldBe(200);

        (await Call(HttpMethod.Post, Base, admin, NewUser("SalesPerson"))).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, Base, admin, NewUser("SalesManager", manager))).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, Base, admin, NewUser("Director"))).Status.ShouldBe(400);
        var notManager = await Call(HttpMethod.Post, Base, admin, NewUser("SalesPerson", person));
        notManager.Status.ShouldBe(400);
        notManager.FirstMessage.ShouldContain("active sales manager");
        (await Call(HttpMethod.Post, Base, admin, NewUser("SalesPerson", inactiveManager))).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, Base, admin, NewUser("SalesPerson", Guid.NewGuid()))).Status.ShouldBe(400);
    }

    [Fact]
    public async Task Create_DuplicatesAndBadInput_AreRefused()
    {
        var admin = await AdminTokenAsync();
        var username = Uniq("dup");
        (await Call(HttpMethod.Post, Base, admin, NewUser("SalesManager", username: username))).Status.ShouldBe(201);

        var dupUser = await Call(HttpMethod.Post, Base, admin, NewUser("SalesManager", username: username, email: $"other{username}@x.example.com"));
        dupUser.Status.ShouldBe(409);
        dupUser.FirstMessage.ShouldContain("username");
        var dupEmail = await Call(HttpMethod.Post, Base, admin, NewUser("SalesManager", email: $"{username}@sales.example.com"));
        dupEmail.Status.ShouldBe(409);
        dupEmail.FirstMessage.ShouldContain("email");

        (await Call(HttpMethod.Post, Base, admin, NewUser("SalesManager", password: "short"))).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, Base, admin, NewUser("SalesManager", confirm: Pwd + "x"))).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, Base, admin, NewUser("SalesManager", username: "a b"))).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, Base, admin, NewUser("SalesManager", email: "not-an-email"))).Status.ShouldBe(400);
    }

    [Fact]
    public async Task ConcurrentCreatesWithSameUsername_OnlyOneSucceeds()
    {
        var admin = await AdminTokenAsync();
        var username = Uniq("race");
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
            Call(HttpMethod.Post, Base, admin, NewUser("SalesManager", username: username, email: $"{username}{i}@race.example.com"))));
        results.Count(r => r.Status == 201).ShouldBe(1);
        results.Count(r => r.Status == 409).ShouldBe(4);
    }

    // ---------------- list ----------------

    [Fact]
    public async Task List_FiltersByTeamRoleAndSearch_AndManagersListShowsActiveManagersOnly()
    {
        var admin = await AdminTokenAsync();
        var manager = await CreateAsync(admin, "SalesManager");
        var a = await CreateAsync(admin, "SalesPerson", manager);
        var b = await CreateAsync(admin, "SalesPerson", manager);

        var team = await Call(HttpMethod.Get, $"{Base}?managerId={manager}", admin);
        team.Status.ShouldBe(200);
        team.Data.GetProperty("totalCount").GetInt32().ShouldBe(2);
        team.Data.GetProperty("items").EnumerateArray().Select(x => Guid.Parse(x.GetProperty("id").GetString()!)).ShouldBe(new[] { a, b }, ignoreOrder: true);

        var username = (await Call(HttpMethod.Get, $"{Base}/{a}", admin)).Data.GetProperty("username").GetString()!;
        var search = await Call(HttpMethod.Get, $"{Base}?search={username}", admin);
        search.Data.GetProperty("totalCount").GetInt32().ShouldBe(1);

        var managersOnly = await Call(HttpMethod.Get, $"{Base}?role=SalesManager&pageSize=100", admin);
        managersOnly.Data.GetProperty("items").EnumerateArray().ShouldAllBe(x => x.GetProperty("role").GetString() == "SalesManager");

        var options = await Call(HttpMethod.Get, $"{Base}/managers", admin);
        options.Data.EnumerateArray().Select(x => Guid.Parse(x.GetProperty("id").GetString()!)).ShouldContain(manager);
        (await Call(HttpMethod.Put, $"{Base}/{manager}/status", admin, new { status = "Inactive" })).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, $"{Base}/managers", admin)).Data.EnumerateArray()
            .Select(x => Guid.Parse(x.GetProperty("id").GetString()!)).ShouldNotContain(manager);
        (await Call(HttpMethod.Get, $"{Base}?managerId={Guid.NewGuid()}", admin)).Data.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    // ---------------- update ----------------

    [Fact]
    public async Task Update_MovesTeam_PromotesAndDemotes_UnderTheRules()
    {
        var admin = await AdminTokenAsync();
        var m1 = await CreateAsync(admin, "SalesManager");
        var m2 = await CreateAsync(admin, "SalesManager");
        var person = await CreateAsync(admin, "SalesPerson", m1);
        var pvBefore = await PermissionVersionAsync(person);

        // Move to another manager: permission version bumps (their User Panel view changes).
        var moved = await Call(HttpMethod.Put, $"{Base}/{person}", admin,
            new { firstName = "Moved", lastName = "Person", email = $"{Uniq("mv")}@x.example.com", role = "SalesPerson", managerId = m2 });
        moved.Status.ShouldBe(200);
        moved.Data.GetProperty("managerId").GetString().ShouldBe(m2.ToString());
        (await PermissionVersionAsync(person)).ShouldBe(pvBefore + 1);

        // A manager with a team cannot become a sales person.
        var blocked = await Call(HttpMethod.Put, $"{Base}/{m2}", admin,
            new { firstName = "M", lastName = "Two", email = $"{Uniq("m2")}@x.example.com", role = "SalesPerson", managerId = m1 });
        blocked.Status.ShouldBe(409);
        blocked.FirstMessage.ShouldContain("1 sales person");

        // Promote the person to manager (no manager), then m2 is empty and can be demoted under m1.
        (await Call(HttpMethod.Put, $"{Base}/{person}", admin,
            new { firstName = "Moved", lastName = "Person", email = $"{Uniq("pr")}@x.example.com", role = "SalesManager" })).Status.ShouldBe(200);
        var demoted = await Call(HttpMethod.Put, $"{Base}/{m2}", admin,
            new { firstName = "M", lastName = "Two", email = $"{Uniq("m2")}@x.example.com", role = "SalesPerson", managerId = m1 });
        demoted.Status.ShouldBe(200);
        demoted.Data.GetProperty("role").GetString().ShouldBe("SalesPerson");
    }

    [Fact]
    public async Task Update_SelfAsManager_TakenEmail_UnknownUser_AreRefused()
    {
        var admin = await AdminTokenAsync();
        var manager = await CreateAsync(admin, "SalesManager");
        var takenEmail = (await Call(HttpMethod.Get, $"{Base}/{manager}", admin)).Data.GetProperty("email").GetString();
        var other = await CreateAsync(admin, "SalesManager");

        (await Call(HttpMethod.Put, $"{Base}/{other}", admin, new { firstName = "a", lastName = "b", email = takenEmail, role = "SalesManager" })).Status.ShouldBe(409);
        (await Call(HttpMethod.Put, $"{Base}/{other}", admin, new { firstName = "a", lastName = "b", email = "ok@x.example.com", role = "SalesPerson", managerId = other })).Status.ShouldBe(400);
        (await Call(HttpMethod.Put, $"{Base}/{Guid.NewGuid()}", admin, new { firstName = "a", lastName = "b", email = "ok@x.example.com", role = "SalesManager" })).Status.ShouldBe(404);
    }

    // ---------------- status / password ----------------

    [Fact]
    public async Task Status_DeactivateAndReactivate_ClearsLockout_AndBumpsVersion()
    {
        var admin = await AdminTokenAsync();
        var manager = await CreateAsync(admin, "SalesManager");
        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.SalesUsers SET FailedLoginCount = 3, LockoutEndUtc = DATEADD(MINUTE, 10, SYSUTCDATETIME()) WHERE PublicId = @p",
            new[] { TestDb.P("@p", manager) });
        (await Call(HttpMethod.Get, $"{Base}/{manager}", admin)).Data.GetProperty("isLockedOut").GetBoolean().ShouldBeTrue();
        var pv = await PermissionVersionAsync(manager);

        var off = await Call(HttpMethod.Put, $"{Base}/{manager}/status", admin, new { status = "Inactive" });
        off.Status.ShouldBe(200);
        off.Data.GetProperty("status").GetString().ShouldBe("Inactive");
        var on = await Call(HttpMethod.Put, $"{Base}/{manager}/status", admin, new { status = "Active" });
        on.Data.GetProperty("status").GetString().ShouldBe("Active");
        on.Data.GetProperty("isLockedOut").GetBoolean().ShouldBeFalse();
        (await PermissionVersionAsync(manager)).ShouldBe(pv + 2);

        (await Call(HttpMethod.Put, $"{Base}/{manager}/status", admin, new { status = "Locked" })).Status.ShouldBe(400);
        (await Call(HttpMethod.Put, $"{Base}/{Guid.NewGuid()}/status", admin, new { status = "Active" })).Status.ShouldBe(404);
    }

    [Fact]
    public async Task ResetPassword_SetsNewHash_ForcesChange_AndIsAuditedWithoutSecrets()
    {
        var admin = await AdminTokenAsync();
        var manager = await CreateAsync(admin, "SalesManager");
        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.SalesUsers SET MustChangePassword = 0 WHERE PublicId = @p", new[] { TestDb.P("@p", manager) });
        var oldHash = await _fx.Db.ExecuteScalarAsync<string>("SELECT PasswordHash FROM dbo.SalesUsers WHERE PublicId = @p", new[] { TestDb.P("@p", manager) });
        var pv = await PermissionVersionAsync(manager);

        var res = await Call(HttpMethod.Post, $"{Base}/{manager}/reset-password", admin, new { newPassword = "N3w#Temp0rary", confirmPassword = "N3w#Temp0rary" });
        res.Status.ShouldBe(200);
        res.FirstMessage.ShouldContain("must change");

        var row = await _fx.Db.QuerySingleOrDefaultAsync("SELECT PasswordHash, MustChangePassword FROM dbo.SalesUsers WHERE PublicId = @p",
            r => (Hash: r.GetString(0), Must: r.GetBoolean(1)), new[] { TestDb.P("@p", manager) });
        row.Hash.ShouldNotBe(oldHash);
        row.Must.ShouldBeTrue();
        (await PermissionVersionAsync(manager)).ShouldBe(pv + 1);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType = 'SalesUser' AND Action = 'PasswordReset' AND EntityId = @id AND OldValues IS NULL AND NewValues IS NULL",
            new[] { TestDb.P("@id", manager.ToString()) })).ShouldBe(1);

        (await Call(HttpMethod.Post, $"{Base}/{manager}/reset-password", admin, new { newPassword = "weak", confirmPassword = "weak" })).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Base}/{Guid.NewGuid()}/reset-password", admin, new { newPassword = Pwd, confirmPassword = Pwd })).Status.ShouldBe(404);
    }

    [Fact]
    public async Task Seed_SalesUserPermissionsExist()
        => (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Permissions WHERE Code LIKE 'SalesUser.%' AND IsActive = 1")).ShouldBe(5);
}
