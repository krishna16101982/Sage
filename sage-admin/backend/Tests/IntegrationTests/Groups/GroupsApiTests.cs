using System.Text.Json;
using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Groups;

[Collection(ApiFixture.CollectionName)]
public class GroupsApiTests
{
    private readonly ApiFixture _fx;
    public GroupsApiTests(ApiFixture fx) { _fx = fx; }

    private const string Base = "/api/admin/groups";
    private static string Uniq(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    private async Task<string> TokenAsync(TestUser u)
        => (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;

    private async Task<(TestUser user, string token)> UserWithAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return (u, await TokenAsync(u));
    }

    private async Task<string> AdminTokenAsync() => await TokenAsync(await TestData.CreateUserAsync(_fx.Db, isSuperAdmin: true));

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    private async Task<(Guid id, string code, string name)> CreateGroupAsync(string token, string? code = null, string? name = null)
    {
        code ??= Uniq("G");
        name ??= "Group " + code;
        var res = await Call(HttpMethod.Post, Base, token, new { code, name, description = "test group" });
        res.Status.ShouldBe(201);
        return (Guid.Parse(res.Data.GetProperty("id").GetString()!), code, name);
    }

    private async Task<long> PermissionIdAsync(string code)
        => await _fx.Db.ExecuteScalarAsync<long>("SELECT Id FROM dbo.Permissions WHERE Code = @c", new[] { TestDb.P("@c", code) });

    private async Task AddMemberAsync(Guid groupId, TestUser user)
        => await _fx.Db.ExecuteNonQueryAsync(
            "INSERT dbo.AdminUserGroups (UserId, GroupId, IsPrimary) SELECT @u, Id, 1 FROM dbo.PermissionGroups WHERE PublicId = @g",
            new[] { TestDb.P("@u", user.Id), TestDb.P("@g", groupId) });

    private static IEnumerable<JsonElement> MatrixPermissions(JsonElement group)
        => group.GetProperty("modules").EnumerateArray().SelectMany(m => m.GetProperty("permissions").EnumerateArray());

    private static List<string> AssignedCodes(JsonElement group)
        => MatrixPermissions(group).Where(p => p.GetProperty("assigned").GetBoolean()).Select(p => p.GetProperty("code").GetString()!).OrderBy(x => x).ToList();

    // ---------------- 401 / 403 ----------------

    [Theory]
    [InlineData("GET", Base)]
    [InlineData("GET", Base + "/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", Base)]
    [InlineData("PUT", Base + "/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", Base + "/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", Base + "/00000000-0000-0000-0000-000000000001/permissions")]
    public async Task EveryEndpoint_WithoutToken_Returns401(string method, string url)
    {
        var res = await Call(new HttpMethod(method), url, null, method is "GET" or "DELETE" ? null : new { });
        res.Status.ShouldBe(401);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("hr")]
    [InlineData("permissionAdmin")]
    public async Task UsersWithoutGroupPermissions_Get403_OnEveryEndpoint(string profile)
    {
        var perms = profile switch
        {
            "hr" => new[] { "Customer.View", "Customer.Add", "Customer.Edit" },
            "permissionAdmin" => new[] { "Permission.View", "Permission.Add", "Permission.Edit" },
            _ => Array.Empty<string>()
        };
        var (_, token) = await UserWithAsync(perms);
        var id = Guid.NewGuid();

        (await Call(HttpMethod.Get, Base, token)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, $"{Base}/{id}", token)).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, Base, token, new { code = Uniq("G"), name = "x" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/{id}", token, new { name = "x", status = "Active" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Delete, $"{Base}/{id}", token)).Status.ShouldBe(403);
        var denied = await Call(HttpMethod.Put, $"{Base}/{id}/permissions", token, new { permissionIds = Array.Empty<long>() });
        denied.Status.ShouldBe(403);
        denied.Root.GetProperty("messageType").GetString().ShouldBe("PERMISSION_DENIED");
    }

    [Fact]
    public async Task EachWriteEndpoint_NeedsItsOwnPermission()
    {
        var (g, _, _) = await CreateGroupAsync(await AdminTokenAsync());

        var (_, viewer) = await UserWithAsync("Group.View");
        (await Call(HttpMethod.Get, Base, viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, $"{Base}/{g}", viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Post, Base, viewer, new { code = Uniq("G"), name = Uniq("n") })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/{g}", viewer, new { name = "x", status = "Active" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Delete, $"{Base}/{g}", viewer)).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/{g}/permissions", viewer, new { permissionIds = Array.Empty<long>() })).Status.ShouldBe(403);

        // Assigning permissions is separate from editing the group.
        var (_, editor) = await UserWithAsync("Group.View", "Group.Edit");
        (await Call(HttpMethod.Put, $"{Base}/{g}/permissions", editor, new { permissionIds = Array.Empty<long>() })).Status.ShouldBe(403);
        var (_, assigner) = await UserWithAsync("Group.View", "Group.AssignPermissions");
        (await Call(HttpMethod.Put, $"{Base}/{g}/permissions", assigner, new { permissionIds = Array.Empty<long>() })).Status.ShouldBe(200);
        (await Call(HttpMethod.Put, $"{Base}/{g}", assigner, new { name = "x", status = "Active" })).Status.ShouldBe(403);
    }

    // ---------------- create ----------------

    [Fact]
    public async Task Create_ReturnsGroup_ActiveWithNoPermissions_AndAudits()
    {
        var token = await AdminTokenAsync();
        var code = Uniq("HR");
        var res = await Call(HttpMethod.Post, Base, token, new { code, name = "HR " + code, description = "  Human resources  " });

        res.Status.ShouldBe(201);
        res.Data.GetProperty("code").GetString().ShouldBe(code);
        res.Data.GetProperty("description").GetString().ShouldBe("Human resources");
        res.Data.GetProperty("status").GetString().ShouldBe("Active");
        res.Data.GetProperty("userCount").GetInt32().ShouldBe(0);
        res.Data.GetProperty("permissionCount").GetInt32().ShouldBe(0);

        var id = res.Data.GetProperty("id").GetString();
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType = 'PermissionGroup' AND Action = 'Create' AND EntityId = @id",
            new[] { TestDb.P("@id", id!) })).ShouldBe(1);
    }

    [Fact]
    public async Task Create_DuplicateCodeOrName_Returns409_WithSpecificMessage()
    {
        var token = await AdminTokenAsync();
        var (_, code, name) = await CreateGroupAsync(token);

        var dupCode = await Call(HttpMethod.Post, Base, token, new { code, name = Uniq("Other") });
        dupCode.Status.ShouldBe(409);
        dupCode.FirstMessage.ShouldContain("code");

        var dupName = await Call(HttpMethod.Post, Base, token, new { code = Uniq("G"), name });
        dupName.Status.ShouldBe(409);
        dupName.FirstMessage.ShouldContain("name");
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("1bad", "Name")]
    [InlineData("has space", "Name")]
    [InlineData("GOOD_CODE", "")]
    public async Task Create_InvalidInput_Returns400(string code, string name)
    {
        var res = await Call(HttpMethod.Post, Base, await AdminTokenAsync(), new { code = code == "GOOD_CODE" ? Uniq("G") : code, name });
        res.Status.ShouldBe(400);
    }

    [Fact]
    public async Task ConcurrentCreatesWithSameCode_OnlyOneSucceeds()
    {
        var token = await AdminTokenAsync();
        var code = Uniq("RACE");
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Call(HttpMethod.Post, Base, token, new { code, name = $"Race {code} {i}" })));
        results.Count(r => r.Status == 201).ShouldBe(1);
        results.Count(r => r.Status == 409).ShouldBe(5);
    }

    // ---------------- read ----------------

    [Fact]
    public async Task Get_ReturnsMatrixOfActivePermissions_NoneAssigned()
    {
        var token = await AdminTokenAsync();
        var (g, _, _) = await CreateGroupAsync(token);

        var res = await Call(HttpMethod.Get, $"{Base}/{g}", token);
        res.Status.ShouldBe(200);
        var modules = res.Data.GetProperty("modules").EnumerateArray().Select(m => m.GetProperty("code").GetString()).ToList();
        modules.ShouldContain("Customer");
        modules.ShouldContain("Group");
        MatrixPermissions(res.Data).Select(p => p.GetProperty("code").GetString()).ShouldContain("Customer.View");
        AssignedCodes(res.Data).ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_UnknownGroup_Returns404()
        => (await Call(HttpMethod.Get, $"{Base}/{Guid.NewGuid()}", await AdminTokenAsync())).Status.ShouldBe(404);

    [Fact]
    public async Task List_SearchStatusAndPaging_Work()
    {
        var token = await AdminTokenAsync();
        var tag = Uniq("Tag");
        var (a, _, _) = await CreateGroupAsync(token, name: $"{tag} Alpha");
        await CreateGroupAsync(token, name: $"{tag} Beta");
        await CreateGroupAsync(token, name: $"{tag} Gamma");
        (await Call(HttpMethod.Put, $"{Base}/{a}", token, new { name = $"{tag} Alpha", status = "Inactive" })).Status.ShouldBe(200);

        var all = await Call(HttpMethod.Get, $"{Base}?search={tag}&pageSize=2", token);
        all.Status.ShouldBe(200);
        all.Data.GetProperty("totalCount").GetInt32().ShouldBe(3);
        all.Data.GetProperty("items").GetArrayLength().ShouldBe(2);
        all.Data.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe($"{tag} Alpha");

        var inactive = await Call(HttpMethod.Get, $"{Base}?search={tag}&status=Inactive", token);
        inactive.Data.GetProperty("totalCount").GetInt32().ShouldBe(1);

        var desc = await Call(HttpMethod.Get, $"{Base}?search={tag}&sortBy=name&sortDirection=desc", token);
        desc.Data.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe($"{tag} Gamma");
    }

    [Fact]
    public async Task List_SearchWithLikeWildcards_IsTreatedAsText()
    {
        var token = await AdminTokenAsync();
        var res = await Call(HttpMethod.Get, $"{Base}?search=%25_%5B", token); // "%_["
        res.Status.ShouldBe(200);
        res.Data.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    // ---------------- update ----------------

    [Fact]
    public async Task Update_ChangesNameDescriptionStatus_ButNotCode()
    {
        var token = await AdminTokenAsync();
        var (g, code, _) = await CreateGroupAsync(token);
        var newName = Uniq("Renamed");

        var res = await Call(HttpMethod.Put, $"{Base}/{g}", token, new { name = newName, description = "new", status = "Inactive", code = "IGNORED" });
        res.Status.ShouldBe(200);
        res.Data.GetProperty("name").GetString().ShouldBe(newName);
        res.Data.GetProperty("status").GetString().ShouldBe("Inactive");
        res.Data.GetProperty("code").GetString().ShouldBe(code);
    }

    [Fact]
    public async Task Update_NameTakenByAnotherGroup_Returns409_AndUnknownReturns404_AndBadStatusReturns400()
    {
        var token = await AdminTokenAsync();
        var (_, _, takenName) = await CreateGroupAsync(token);
        var (g, _, _) = await CreateGroupAsync(token);

        (await Call(HttpMethod.Put, $"{Base}/{g}", token, new { name = takenName, status = "Active" })).Status.ShouldBe(409);
        (await Call(HttpMethod.Put, $"{Base}/{Guid.NewGuid()}", token, new { name = "x", status = "Active" })).Status.ShouldBe(404);
        (await Call(HttpMethod.Put, $"{Base}/{g}", token, new { name = "x", status = "Locked" })).Status.ShouldBe(400);
    }

    [Fact]
    public async Task SystemGroup_CannotBeDeactivatedOrDeleted()
    {
        var token = await AdminTokenAsync();
        var (g, _, name) = await CreateGroupAsync(token);
        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.PermissionGroups SET IsSystem = 1 WHERE PublicId = @g", new[] { TestDb.P("@g", g) });

        (await Call(HttpMethod.Put, $"{Base}/{g}", token, new { name, status = "Inactive" })).Status.ShouldBe(400);
        (await Call(HttpMethod.Delete, $"{Base}/{g}", token)).Status.ShouldBe(400);
        (await Call(HttpMethod.Put, $"{Base}/{g}", token, new { name = name + " x", status = "Active" })).Status.ShouldBe(200);
    }

    // ---------------- permissions ----------------

    [Fact]
    public async Task SetPermissions_ReplacesTheSet_AndAuditsAddedAndRemoved()
    {
        var token = await AdminTokenAsync();
        var (g, _, _) = await CreateGroupAsync(token);
        var view = await PermissionIdAsync("Customer.View");
        var add = await PermissionIdAsync("Customer.Add");
        var edit = await PermissionIdAsync("Customer.Edit");

        var first = await Call(HttpMethod.Put, $"{Base}/{g}/permissions", token, new { permissionIds = new[] { view, add, add } });
        first.Status.ShouldBe(200);
        AssignedCodes(first.Data).ShouldBe(new[] { "Customer.Add", "Customer.View" });
        first.Data.GetProperty("permissionCount").GetInt32().ShouldBe(2);

        var second = await Call(HttpMethod.Put, $"{Base}/{g}/permissions", token, new { permissionIds = new[] { view, edit } });
        AssignedCodes(second.Data).ShouldBe(new[] { "Customer.Edit", "Customer.View" });

        var audit = await _fx.Db.QueryAsync(
            "SELECT NewValues FROM dbo.AuditLogs WHERE EntityType = 'PermissionGroup' AND Action = 'AssignPermissions' AND EntityId = @id ORDER BY Id",
            r => r.GetString(0), new[] { TestDb.P("@id", g.ToString()) });
        audit.Count.ShouldBe(2);
        using var last = JsonDocument.Parse(audit[1]);
        last.RootElement.GetProperty("Added").EnumerateArray().Select(x => x.GetString()).ShouldBe(new[] { "Customer.Edit" });
        last.RootElement.GetProperty("Removed").EnumerateArray().Select(x => x.GetString()).ShouldBe(new[] { "Customer.Add" });

        var cleared = await Call(HttpMethod.Put, $"{Base}/{g}/permissions", token, new { permissionIds = Array.Empty<long>() });
        AssignedCodes(cleared.Data).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetPermissions_SameSetAgain_WritesNoAuditAndKeepsMembersSignedIn()
    {
        var token = await AdminTokenAsync();
        var (g, _, _) = await CreateGroupAsync(token);
        var view = await PermissionIdAsync("Group.View");
        (await Call(HttpMethod.Put, $"{Base}/{g}/permissions", token, new { permissionIds = new[] { view } })).Status.ShouldBe(200);

        var member = await TestData.CreateUserAsync(_fx.Db);
        await AddMemberAsync(g, member);
        var memberToken = await TokenAsync(member);

        (await Call(HttpMethod.Put, $"{Base}/{g}/permissions", token, new { permissionIds = new[] { view } })).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, Base, memberToken)).Status.ShouldBe(200);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE Action = 'AssignPermissions' AND EntityId = @id",
            new[] { TestDb.P("@id", g.ToString()) })).ShouldBe(1);
    }

    [Fact]
    public async Task SetPermissions_UnknownId_Returns400_AndNothingChanges()
    {
        var token = await AdminTokenAsync();
        var (g, _, _) = await CreateGroupAsync(token);
        var view = await PermissionIdAsync("Customer.View");

        var res = await Call(HttpMethod.Put, $"{Base}/{g}/permissions", token, new { permissionIds = new[] { view, 999999999L } });
        res.Status.ShouldBe(400);
        AssignedCodes((await Call(HttpMethod.Get, $"{Base}/{g}", token)).Data).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetPermissions_InactivePermission_CannotBeGranted_ButCanBeKept()
    {
        var token = await AdminTokenAsync();
        var (g, _, _) = await CreateGroupAsync(token);
        var moduleRes = await Call(HttpMethod.Post, "/api/admin/permissions/modules", token, new { code = Uniq("M"), name = "Temp", sortOrder = 999 });
        var moduleId = moduleRes.Data.GetProperty("id").GetInt64();
        var permRes = await Call(HttpMethod.Post, "/api/admin/permissions", token, new { moduleId, action = "View", name = "Temp view" });
        var permId = permRes.Data.GetProperty("id").GetInt64();

        (await Call(HttpMethod.Put, $"{Base}/{g}/permissions", token, new { permissionIds = new[] { permId } })).Status.ShouldBe(200);
        (await Call(HttpMethod.Put, $"/api/admin/permissions/{permId}", token, new { name = "Temp view", isActive = false })).Status.ShouldBe(200);

        // Still held, so it is shown (marked inactive) and may be kept.
        var detail = await Call(HttpMethod.Get, $"{Base}/{g}", token);
        var held = MatrixPermissions(detail.Data).Single(p => p.GetProperty("id").GetInt64() == permId);
        held.GetProperty("assigned").GetBoolean().ShouldBeTrue();
        held.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        (await Call(HttpMethod.Put, $"{Base}/{g}/permissions", token, new { permissionIds = new[] { permId } })).Status.ShouldBe(200);

        // A different group cannot newly receive it.
        var (other, _, _) = await CreateGroupAsync(token);
        var refused = await Call(HttpMethod.Put, $"{Base}/{other}/permissions", token, new { permissionIds = new[] { permId } });
        refused.Status.ShouldBe(400);
        refused.FirstMessage.ShouldContain("Inactive");
    }

    [Fact]
    public async Task SetPermissions_UnknownGroup_Returns404_AndMissingBodyReturns400()
    {
        var token = await AdminTokenAsync();
        (await Call(HttpMethod.Put, $"{Base}/{Guid.NewGuid()}/permissions", token, new { permissionIds = Array.Empty<long>() })).Status.ShouldBe(404);
        var (g, _, _) = await CreateGroupAsync(token);
        (await Call(HttpMethod.Put, $"{Base}/{g}/permissions", token, new { permissionIds = (long[]?)null })).Status.ShouldBe(400);
    }

    // ---------------- effect on members ----------------

    [Fact]
    public async Task GrantingAndRevoking_TakesEffectForMembers_Immediately()
    {
        var admin = await AdminTokenAsync();
        var (g, _, name) = await CreateGroupAsync(admin);
        var member = await TestData.CreateUserAsync(_fx.Db);
        await AddMemberAsync(g, member);

        var before = await TokenAsync(member);
        (await Call(HttpMethod.Get, "/api/admin/permissions", before)).Status.ShouldBe(403);

        // Grant: the member's old token is rejected (permission version changed); a fresh session has the access.
        (await Call(HttpMethod.Put, $"{Base}/{g}/permissions", admin, new { permissionIds = new[] { await PermissionIdAsync("Permission.View") } })).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, "/api/admin/permissions", before)).Status.ShouldBe(401);
        var granted = await TokenAsync(member);
        (await Call(HttpMethod.Get, "/api/admin/permissions", granted)).Status.ShouldBe(200);
        (await AuthHttp.MeAsync(_fx.CreateClient(), granted)).Data.GetProperty("permissions").EnumerateArray()
            .Select(x => x.GetString()).ShouldContain("Permission.View");

        // Deactivating the group removes what it grants.
        (await Call(HttpMethod.Put, $"{Base}/{g}", admin, new { name, status = "Inactive" })).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, "/api/admin/permissions", granted)).Status.ShouldBe(401);
        (await Call(HttpMethod.Get, "/api/admin/permissions", await TokenAsync(member))).Status.ShouldBe(403);
    }

    // ---------------- delete ----------------

    [Fact]
    public async Task Delete_GroupWithMembers_Returns409_ThenSucceedsOnceEmpty()
    {
        var token = await AdminTokenAsync();
        var (g, _, _) = await CreateGroupAsync(token);
        (await Call(HttpMethod.Put, $"{Base}/{g}/permissions", token, new { permissionIds = new[] { await PermissionIdAsync("Customer.View") } })).Status.ShouldBe(200);
        var member = await TestData.CreateUserAsync(_fx.Db);
        await AddMemberAsync(g, member);

        var blocked = await Call(HttpMethod.Delete, $"{Base}/{g}", token);
        blocked.Status.ShouldBe(409);
        blocked.FirstMessage.ShouldContain("users assigned");

        await _fx.Db.ExecuteNonQueryAsync("DELETE dbo.AdminUserGroups WHERE UserId = @u", new[] { TestDb.P("@u", member.Id) });
        (await Call(HttpMethod.Delete, $"{Base}/{g}", token)).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, $"{Base}/{g}", token)).Status.ShouldBe(404);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType = 'PermissionGroup' AND Action = 'Delete' AND EntityId = @id",
            new[] { TestDb.P("@id", g.ToString()) })).ShouldBe(1);
    }

    [Fact]
    public async Task Delete_UnknownGroup_Returns404()
        => (await Call(HttpMethod.Delete, $"{Base}/{Guid.NewGuid()}", await AdminTokenAsync())).Status.ShouldBe(404);
}
