using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Permissions;

[Collection(ApiFixture.CollectionName)]
public class PermissionsApiTests
{
    private readonly ApiFixture _fx;
    public PermissionsApiTests(ApiFixture fx) { _fx = fx; }

    private static string Uniq(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    private async Task<string> TokenAsync(TestUser u)
        => (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;

    private async Task<(TestUser user, string token)> UserWithAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return (u, await TokenAsync(u));
    }

    private async Task<string> AdminTokenAsync()
        => await TokenAsync(await TestData.CreateUserAsync(_fx.Db, isSuperAdmin: true));

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    private async Task<long> CreateModuleAsync(string token, string? code = null)
    {
        var res = await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = code ?? Uniq("M"), name = "Test Module", category = "Test", sortOrder = 900 });
        res.Status.ShouldBe(201);
        return res.Data.GetProperty("id").GetInt64();
    }

    private async Task<long> CreatePermissionAsync(string token, long moduleId, string? action = null)
    {
        var res = await Call(HttpMethod.Post, "/api/admin/permissions", token, new { moduleId, action = action ?? "View", name = "Test permission", description = "d" });
        res.Status.ShouldBe(201);
        return res.Data.GetProperty("id").GetInt64();
    }

    // ---------------- 401 / 403 ----------------

    [Theory]
    [InlineData("GET", "/api/admin/permissions")]
    [InlineData("GET", "/api/admin/permissions/modules")]
    [InlineData("POST", "/api/admin/permissions/modules")]
    [InlineData("PUT", "/api/admin/permissions/modules/1")]
    [InlineData("POST", "/api/admin/permissions")]
    [InlineData("PUT", "/api/admin/permissions/1")]
    public async Task EveryEndpoint_WithoutToken_Returns401(string method, string url)
    {
        var res = await Call(new HttpMethod(method), url, null, method == "GET" ? null : new { });
        res.Status.ShouldBe(401);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("hr")]
    [InlineData("sales")]
    [InlineData("accounts")]
    public async Task UsersWithoutPermissionPermissions_Get403_OnEveryEndpoint(string profile)
    {
        var perms = profile switch
        {
            "hr" => new[] { "Customer.View", "Customer.Add", "Customer.Edit" },
            "sales" => new[] { "Customer.View", "Sales.View", "Sales.Add" },
            "accounts" => new[] { "Sales.View", "Product.View", "SalesUser.View" },
            _ => Array.Empty<string>()
        };
        var (_, token) = await UserWithAsync(perms);

        (await Call(HttpMethod.Get, "/api/admin/permissions", token)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, "/api/admin/permissions/modules", token)).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = Uniq("X"), name = "x" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, "/api/admin/permissions", token, new { moduleId = 1, action = "Zed", name = "x" })).Status.ShouldBe(403);
        var denied = await Call(HttpMethod.Put, "/api/admin/permissions/1", token, new { name = "x", isActive = true });
        denied.Status.ShouldBe(403);
        denied.Root.GetProperty("messageType").GetString().ShouldBe("PERMISSION_DENIED");
    }

    [Fact]
    public async Task ViewOnlyUser_CanRead_ButCannotCreateOrEdit()
    {
        var (_, token) = await UserWithAsync("Permission.View");
        (await Call(HttpMethod.Get, "/api/admin/permissions", token)).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, "/api/admin/permissions/modules", token)).Status.ShouldBe(200);
        (await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = Uniq("X"), name = "x" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, "/api/admin/permissions", token, new { moduleId = 1, action = "Zed", name = "x" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, "/api/admin/permissions/1", token, new { name = "x", isActive = true })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, "/api/admin/permissions/modules/1", token, new { name = "x", isActive = true })).Status.ShouldBe(403);
    }

    [Fact]
    public async Task SuperAdmin_CanUseEveryEndpoint()
    {
        var token = await AdminTokenAsync();
        (await Call(HttpMethod.Get, "/api/admin/permissions", token)).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, "/api/admin/permissions/modules", token)).Status.ShouldBe(200);
    }

    [Fact]
    public async Task InactiveSuperAdmin_IsLockedOut()
    {
        var u = await TestData.CreateUserAsync(_fx.Db, isSuperAdmin: true);
        var token = await TokenAsync(u);
        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.AdminUsers SET Status='Inactive' WHERE Id=@i", new[] { TestDb.P("@i", u.Id) });
        (await Call(HttpMethod.Get, "/api/admin/permissions", token)).Status.ShouldBe(401);
    }

    // ---------------- list / filter / paging ----------------

    [Fact]
    public async Task List_ReturnsSeededPermissions_WithModuleInfoAndPagingMetadata()
    {
        var res = await Call(HttpMethod.Get, "/api/admin/permissions?pageSize=10", await AdminTokenAsync());
        res.Status.ShouldBe(200);
        res.Data.GetProperty("items").GetArrayLength().ShouldBe(10);
        res.Data.GetProperty("totalCount").GetInt32().ShouldBeGreaterThan(30);
        res.Data.GetProperty("totalPages").GetInt32().ShouldBeGreaterThan(1);
        var first = res.Data.GetProperty("items")[0];
        foreach (var p in new[] { "id", "moduleId", "moduleCode", "moduleName", "action", "code", "name", "isActive", "isSystem" })
            first.TryGetProperty(p, out _).ShouldBeTrue(p);
    }

    [Fact]
    public async Task List_PagesDoNotOverlap_AndCoverEverything()
    {
        var token = await AdminTokenAsync();
        var all = await Call(HttpMethod.Get, "/api/admin/permissions?pageSize=200&sortBy=code", token);
        var total = all.Data.GetProperty("totalCount").GetInt32();
        var seen = new HashSet<long>();
        for (var page = 1; page <= (int)Math.Ceiling(total / 7.0); page++)
        {
            var r = await Call(HttpMethod.Get, $"/api/admin/permissions?page={page}&pageSize=7&sortBy=code", token);
            foreach (var item in r.Data.GetProperty("items").EnumerateArray()) seen.Add(item.GetProperty("id").GetInt64()).ShouldBeTrue();
        }
        seen.Count.ShouldBe(total);
    }

    [Fact]
    public async Task List_FiltersByModule()
    {
        var token = await AdminTokenAsync();
        var moduleId = await _fx.Db.ExecuteScalarAsync<long>("SELECT Id FROM dbo.Modules WHERE Code='Customer'");
        var res = await Call(HttpMethod.Get, $"/api/admin/permissions?moduleId={moduleId}&pageSize=50", token);
        var codes = res.Data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()!).ToList();
        codes.Count.ShouldBe(4);
        codes.ShouldAllBe(c => c.StartsWith("Customer."));
    }

    [Fact]
    public async Task List_SearchMatchesCodeNameOrModule_CaseInsensitive()
    {
        var token = await AdminTokenAsync();
        var res = await Call(HttpMethod.Get, "/api/admin/permissions?search=customer&pageSize=50", token);
        var codes = res.Data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()!).ToList();
        codes.Count.ShouldBe(4);
        codes.ShouldAllBe(c => c.StartsWith("Customer."));
    }

    [Fact]
    public async Task List_SearchTreatsWildcardsLiterally()
    {
        var token = await AdminTokenAsync();
        var res = await Call(HttpMethod.Get, "/api/admin/permissions?search=%25", token);   // "%"
        res.Status.ShouldBe(200);
        res.Data.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task List_SortIsAllowListed_InjectionAttemptIsIgnored()
    {
        var token = await AdminTokenAsync();
        var res = await Call(HttpMethod.Get, "/api/admin/permissions?sortBy=Code;DROP%20TABLE%20dbo.Permissions;--&pageSize=5", token);
        res.Status.ShouldBe(200);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name='Permissions'")).ShouldBe(1);
    }

    [Fact]
    public async Task List_SortsByCodeAscAndDesc()
    {
        var token = await AdminTokenAsync();
        async Task<List<string>> Codes(string dir)
        {
            var r = await Call(HttpMethod.Get, $"/api/admin/permissions?sortBy=code&sortDirection={dir}&pageSize=200", token);
            return r.Data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()!).ToList();
        }
        var asc = await Codes("asc");
        var desc = await Codes("desc");
        asc.ShouldBe(asc.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
        desc.ShouldBe(asc.AsEnumerable().Reverse().ToList());
    }

    [Fact]
    public async Task List_PageSizeIsCapped()
    {
        var res = await Call(HttpMethod.Get, "/api/admin/permissions?pageSize=100000&page=-4", await AdminTokenAsync());
        res.Data.GetProperty("pageSize").GetInt32().ShouldBe(200);
        res.Data.GetProperty("page").GetInt32().ShouldBe(1);
    }

    // ---------------- modules ----------------

    [Fact]
    public async Task Modules_ReturnNestedPermissions_ForTheGroupMatrix()
    {
        var res = await Call(HttpMethod.Get, "/api/admin/permissions/modules", await AdminTokenAsync());
        res.Status.ShouldBe(200);
        var employee = res.Data.EnumerateArray().First(m => m.GetProperty("code").GetString() == "Customer");
        employee.GetProperty("permissions").GetArrayLength().ShouldBe(4);
        employee.GetProperty("permissionCount").GetInt32().ShouldBe(4);
    }

    [Fact]
    public async Task Modules_InactiveAreHiddenUnlessRequested()
    {
        var token = await AdminTokenAsync();
        var id = await CreateModuleAsync(token);
        var permId = await CreatePermissionAsync(token, id);
        (await Call(HttpMethod.Put, $"/api/admin/permissions/modules/{id}", token, new { name = "Off", category = "Test", sortOrder = 900, isActive = false })).Status.ShouldBe(200);

        var def = await Call(HttpMethod.Get, "/api/admin/permissions/modules", token);
        def.Data.EnumerateArray().Any(m => m.GetProperty("id").GetInt64() == id).ShouldBeFalse();
        var inc = await Call(HttpMethod.Get, "/api/admin/permissions/modules?includeInactive=true", token);
        inc.Data.EnumerateArray().Any(m => m.GetProperty("id").GetInt64() == id).ShouldBeTrue();
        permId.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CreateModule_Succeeds_With201_NotSystem_AndIsAudited()
    {
        var token = await AdminTokenAsync();
        var code = Uniq("Inv");
        var res = await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code, name = "Inventory", category = "Ops", sortOrder = 5 });

        res.Status.ShouldBe(201);
        res.Data.GetProperty("code").GetString().ShouldBe(code);
        res.Data.GetProperty("isSystem").GetBoolean().ShouldBeFalse();
        res.Data.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        var id = res.Data.GetProperty("id").GetInt64();
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE Action='Create' AND EntityType='Module' AND EntityId=@i AND ActorUserId IS NOT NULL", new[] { TestDb.P("@i", id.ToString()) })).ShouldBe(1);
    }

    [Fact]
    public async Task CreateModule_DuplicateCode_Returns409_EvenWithDifferentCase()
    {
        var token = await AdminTokenAsync();
        var code = Uniq("Dup");
        await CreateModuleAsync(token, code);
        var res = await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = code.ToUpperInvariant(), name = "Again" });
        res.Status.ShouldBe(409);
        res.Root.GetProperty("messageType").GetString().ShouldBe("CONFLICT");
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1abc")]
    [InlineData("Bad.Code")]
    [InlineData("has space")]
    [InlineData("x'; DROP TABLE dbo.Modules;--")]
    public async Task CreateModule_InvalidCode_Returns400(string code)
    {
        var res = await Call(HttpMethod.Post, "/api/admin/permissions/modules", await AdminTokenAsync(), new { code, name = "x" });
        res.Status.ShouldBe(400);
        res.Root.GetProperty("messageType").GetString().ShouldBe("VALIDATION_ERROR");
    }

    [Fact]
    public async Task CreateModule_MissingNameOrTooLongFields_Returns400()
    {
        var token = await AdminTokenAsync();
        (await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = Uniq("N"), name = "" })).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = Uniq("N"), name = new string('a', 101) })).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = Uniq("N"), name = "ok", category = new string('c', 101) })).Status.ShouldBe(400);
    }

    [Fact]
    public async Task MalformedJsonBody_Returns400WithTheCommonEnvelope()
    {
        var client = _fx.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/permissions/modules") { Content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json") };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await AdminTokenAsync());
        var res = await client.SendAsync(req);
        res.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
        var body = await res.Content.ReadAsStringAsync();
        body.ShouldContain("\"messageType\":\"VALIDATION_ERROR\"");
        body.ShouldNotContain("stackTrace", Case.Insensitive);
    }

    [Fact]
    public async Task CreateModule_StripsHtmlFromNames()
    {
        var token = await AdminTokenAsync();
        var res = await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = Uniq("Xss"), name = "<script>alert(1)</script>Safe", category = "<b>Cat</b>" });
        res.Status.ShouldBe(201);
        res.Data.GetProperty("name").GetString().ShouldBe("alert(1)Safe");
        res.Data.GetProperty("category").GetString().ShouldBe("Cat");
    }

    [Fact]
    public async Task UpdateModule_ChangesEditableFields_ButNeverTheCode()
    {
        var token = await AdminTokenAsync();
        var code = Uniq("Ed");
        var id = await CreateModuleAsync(token, code);
        var res = await Call(HttpMethod.Put, $"/api/admin/permissions/modules/{id}", token, new { code = "Hacked", name = "Renamed", category = "New", sortOrder = 12, isActive = true });

        res.Status.ShouldBe(200);
        res.Data.GetProperty("name").GetString().ShouldBe("Renamed");
        res.Data.GetProperty("sortOrder").GetInt32().ShouldBe(12);
        res.Data.GetProperty("code").GetString().ShouldBe(code);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE Action='Update' AND EntityType='Module' AND EntityId=@i AND OldValues IS NOT NULL AND NewValues IS NOT NULL", new[] { TestDb.P("@i", id.ToString()) })).ShouldBe(1);
    }

    [Fact]
    public async Task UpdateModule_Unknown_Returns404()
        => (await Call(HttpMethod.Put, "/api/admin/permissions/modules/99999999", await AdminTokenAsync(), new { name = "x", isActive = true })).Status.ShouldBe(404);

    [Fact]
    public async Task UpdateModule_SystemModuleCannotBeDeactivated_ButCanBeRenamed()
    {
        var token = await AdminTokenAsync();
        var id = await _fx.Db.ExecuteScalarAsync<long>("SELECT Id FROM dbo.Modules WHERE Code='Group'");
        (await Call(HttpMethod.Put, $"/api/admin/permissions/modules/{id}", token, new { name = "Permission Groups", sortOrder = 20, isActive = false })).Status.ShouldBe(400);
        (await Call(HttpMethod.Put, $"/api/admin/permissions/modules/{id}", token, new { name = "Permission Groups", category = "Administration", sortOrder = 20, isActive = true })).Status.ShouldBe(200);
    }

    [Fact]
    public async Task DeactivatingAModule_RemovesItsPermissionsFromUsersImmediately()
    {
        var admin = await AdminTokenAsync();
        var moduleCode = Uniq("Mod");
        var moduleId = await CreateModuleAsync(admin, moduleCode);
        await CreatePermissionAsync(admin, moduleId, "View");
        var (user, token) = await UserWithAsync();
        await TestData.AssignGroupAsync(_fx.Db, user, moduleCode + ".View");
        token = await TokenAsync(user);
        var c = _fx.CreateClient();
        (await AuthHttp.MeAsync(c, token)).Data.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ShouldContain(moduleCode + ".View");

        await Call(HttpMethod.Put, $"/api/admin/permissions/modules/{moduleId}", admin, new { name = "Off", sortOrder = 1, isActive = false });

        (await AuthHttp.MeAsync(c, token)).Status.ShouldBe(401);                       // old token no longer valid
        var fresh = await TokenAsync(user);
        (await AuthHttp.MeAsync(c, fresh)).Data.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ShouldNotContain(moduleCode + ".View");
    }

    // ---------------- permissions ----------------

    [Fact]
    public async Task CreatePermission_DerivesCodeFromModuleAndAction_And201()
    {
        var token = await AdminTokenAsync();
        var code = Uniq("Pm");
        var moduleId = await CreateModuleAsync(token, code);
        var res = await Call(HttpMethod.Post, "/api/admin/permissions", token, new { moduleId, action = "Approve", name = "Approve things", description = "Can approve" });

        res.Status.ShouldBe(201);
        res.Data.GetProperty("code").GetString().ShouldBe(code + ".Approve");
        res.Data.GetProperty("moduleCode").GetString().ShouldBe(code);
        res.Data.GetProperty("isSystem").GetBoolean().ShouldBeFalse();
        var id = res.Data.GetProperty("id").GetInt64();
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE Action='Create' AND EntityType='Permission' AND EntityId=@i", new[] { TestDb.P("@i", id.ToString()) })).ShouldBe(1);
    }

    [Fact]
    public async Task CreatePermission_Duplicate_Returns409()
    {
        var token = await AdminTokenAsync();
        var moduleId = await CreateModuleAsync(token);
        await CreatePermissionAsync(token, moduleId, "View");
        var res = await Call(HttpMethod.Post, "/api/admin/permissions", token, new { moduleId, action = "View", name = "again" });
        res.Status.ShouldBe(409);
    }

    [Fact]
    public async Task CreatePermission_ExistingSeededCode_Returns409()
    {
        var moduleId = await _fx.Db.ExecuteScalarAsync<long>("SELECT Id FROM dbo.Modules WHERE Code='Customer'");
        var res = await Call(HttpMethod.Post, "/api/admin/permissions", await AdminTokenAsync(), new { moduleId, action = "View", name = "dup" });
        res.Status.ShouldBe(409);
    }

    [Fact]
    public async Task CreatePermission_UnknownModule_Returns404()
        => (await Call(HttpMethod.Post, "/api/admin/permissions", await AdminTokenAsync(), new { moduleId = 99999999, action = "View", name = "x" })).Status.ShouldBe(404);

    [Fact]
    public async Task CreatePermission_InactiveModule_Returns400()
    {
        var token = await AdminTokenAsync();
        var moduleId = await CreateModuleAsync(token);
        await Call(HttpMethod.Put, $"/api/admin/permissions/modules/{moduleId}", token, new { name = "Off", isActive = false });
        (await Call(HttpMethod.Post, "/api/admin/permissions", token, new { moduleId, action = "View", name = "x" })).Status.ShouldBe(400);
    }

    [Theory]
    [InlineData("")]
    [InlineData("V")]
    [InlineData("Bad.Action")]
    [InlineData("1View")]
    [InlineData("a b")]
    public async Task CreatePermission_InvalidAction_Returns400(string action)
    {
        var token = await AdminTokenAsync();
        var moduleId = await CreateModuleAsync(token);
        (await Call(HttpMethod.Post, "/api/admin/permissions", token, new { moduleId, action, name = "x" })).Status.ShouldBe(400);
    }

    [Fact]
    public async Task CreatePermission_MissingNameOrLongDescription_Returns400()
    {
        var token = await AdminTokenAsync();
        var moduleId = await CreateModuleAsync(token);
        (await Call(HttpMethod.Post, "/api/admin/permissions", token, new { moduleId, action = "View", name = "" })).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, "/api/admin/permissions", token, new { moduleId, action = "View", name = "x", description = new string('d', 501) })).Status.ShouldBe(400);
    }

    [Fact]
    public async Task UpdatePermission_ChangesNameAndDescription_NotModuleOrAction()
    {
        var token = await AdminTokenAsync();
        var moduleId = await CreateModuleAsync(token);
        var id = await CreatePermissionAsync(token, moduleId, "View");
        var res = await Call(HttpMethod.Put, $"/api/admin/permissions/{id}", token, new { action = "Hacked", moduleId = 1, name = "Renamed", description = "New text", isActive = true });

        res.Status.ShouldBe(200);
        res.Data.GetProperty("name").GetString().ShouldBe("Renamed");
        res.Data.GetProperty("description").GetString().ShouldBe("New text");
        res.Data.GetProperty("action").GetString().ShouldBe("View");
        res.Data.GetProperty("moduleId").GetInt64().ShouldBe(moduleId);
    }

    [Fact]
    public async Task UpdatePermission_Unknown_Returns404_AndBlankNameReturns400()
    {
        var token = await AdminTokenAsync();
        (await Call(HttpMethod.Put, "/api/admin/permissions/99999999", token, new { name = "x", isActive = true })).Status.ShouldBe(404);
        var moduleId = await CreateModuleAsync(token);
        var id = await CreatePermissionAsync(token, moduleId);
        (await Call(HttpMethod.Put, $"/api/admin/permissions/{id}", token, new { name = " ", isActive = true })).Status.ShouldBe(400);
    }

    [Fact]
    public async Task UpdatePermission_SystemPermissionCannotBeDeactivated()
    {
        var id = await _fx.Db.ExecuteScalarAsync<long>("SELECT Id FROM dbo.Permissions WHERE Code='Permission.Edit'");
        var res = await Call(HttpMethod.Put, $"/api/admin/permissions/{id}", await AdminTokenAsync(), new { name = "Permission - Edit", isActive = false });
        res.Status.ShouldBe(400);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Permissions WHERE Id=@i AND IsActive=1", new[] { TestDb.P("@i", id) })).ShouldBe(1);
    }

    [Fact]
    public async Task DeactivatingAPermission_TakesEffectImmediately_AndReactivatingRestoresIt()
    {
        var admin = await AdminTokenAsync();
        var moduleCode = Uniq("Live");
        var moduleId = await CreateModuleAsync(admin, moduleCode);
        var permId = await CreatePermissionAsync(admin, moduleId, "View");
        var (user, _) = await UserWithAsync();
        await TestData.AssignGroupAsync(_fx.Db, user, moduleCode + ".View");
        var c = _fx.CreateClient();
        var token = await TokenAsync(user);
        (await AuthHttp.MeAsync(c, token)).Status.ShouldBe(200);

        await Call(HttpMethod.Put, $"/api/admin/permissions/{permId}", admin, new { name = "Test permission", isActive = false });
        (await AuthHttp.MeAsync(c, token)).Status.ShouldBe(401);   // version bumped: stale token rejected
        var after = await TokenAsync(user);
        (await AuthHttp.MeAsync(c, after)).Data.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ShouldNotContain(moduleCode + ".View");
        (await _fx.Db.ExecuteScalarAsync<bool>("SELECT dbo.fn_UserHasPermission(@u, @c)", new[] { TestDb.P("@u", user.Id), TestDb.P("@c", moduleCode + ".View") })).ShouldBeFalse();

        await Call(HttpMethod.Put, $"/api/admin/permissions/{permId}", admin, new { name = "Test permission", isActive = true });
        var restored = await TokenAsync(user);
        (await AuthHttp.MeAsync(c, restored)).Data.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ShouldContain(moduleCode + ".View");
    }

    [Fact]
    public async Task Permission_StatusChange_IsAuditedWithOldAndNewValues()
    {
        var token = await AdminTokenAsync();
        var moduleId = await CreateModuleAsync(token);
        var id = await CreatePermissionAsync(token, moduleId);
        await Call(HttpMethod.Put, $"/api/admin/permissions/{id}", token, new { name = "Test permission", isActive = false });
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE Action='StatusChange' AND EntityType='Permission' AND EntityId=@i AND OldValues LIKE '%\"IsActive\":true%' AND NewValues LIKE '%\"IsActive\":false%'",
            new[] { TestDb.P("@i", id.ToString()) })).ShouldBe(1);
    }

    // ---------------- guard rails ----------------

    [Fact]
    public async Task EveryPermissionConstantUsedByTheApi_ExistsInSeedData()
    {
        var constants = typeof(SageAdminApi.Globals.PermissionConstants)
            .GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();
        constants.ShouldNotBeEmpty();
        foreach (var code in constants)
            (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Permissions WHERE Code=@c", new[] { TestDb.P("@c", code) })).ShouldBe(1, code);
    }

    // ---------------- menu link & HR removal ----------------

    [Fact]
    public async Task Module_MenuLink_Validated_KeptWhenNotSent_ClearedWhenEmpty_AndDrivesTheMenu()
    {
        var token = await AdminTokenAsync();
        var code = Uniq("Inv");
        (await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = Uniq("B"), name = "Bad", sortOrder = 1, menuPath = "inventory" })).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = Uniq("B"), name = "Bad", sortOrder = 1, menuPath = "/In ventory" })).Status.ShouldBe(400);

        var created = await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code, name = "Inventory", category = "Stock", sortOrder = 950, menuPath = "/inventory" });
        created.Status.ShouldBe(201);
        created.Data.GetProperty("menuPath").GetString().ShouldBe("/inventory");
        var id = created.Data.GetProperty("id").GetInt64();

        // In the menu only once the module has a View permission.
        async Task<List<string?>> MenuAsync() => (await Call(HttpMethod.Get, "/api/admin/auth/me", await AdminTokenAsync()))
            .Data.GetProperty("menu").EnumerateArray().Select(m => m.GetProperty("path").GetString()).ToList();
        (await MenuAsync()).ShouldNotContain("/inventory");
        await CreatePermissionAsync(token, id, "View");
        (await MenuAsync()).ShouldContain("/inventory");

        // An edit that does not send menuPath (e.g. an older client) keeps the link; an empty one removes it.
        var kept = await Call(HttpMethod.Put, $"/api/admin/permissions/modules/{id}", token, new { name = "Inventory", category = "Stock", sortOrder = 950, isActive = true });
        kept.Data.GetProperty("menuPath").GetString().ShouldBe("/inventory");
        var cleared = await Call(HttpMethod.Put, $"/api/admin/permissions/modules/{id}", token, new { name = "Inventory", category = "Stock", sortOrder = 950, isActive = true, menuPath = "" });
        cleared.Data.GetProperty("menuPath").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        (await MenuAsync()).ShouldNotContain("/inventory");
    }

    [Fact]
    public async Task HrAndAccountsModules_AreRemoved()
    {
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Modules WHERE Category = N'Accounts' OR Code IN ('Invoice','Payment','FinancialReports')")).ShouldBe(0);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Modules WHERE Category = N'HR' OR Code IN ('Employee','Attendance','Payroll')")).ShouldBe(0);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Permissions WHERE Code LIKE 'Employee.%' OR Code LIKE 'Attendance.%' OR Code LIKE 'Payroll.%'")).ShouldBe(0);
    }
}
