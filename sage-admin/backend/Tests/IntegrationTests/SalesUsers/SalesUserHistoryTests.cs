using System.Text.Json;
using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.SalesUsers;

/// <summary>History of sales users: created, activated / deactivated, manager changes, from both panels.</summary>
[Collection(ApiFixture.CollectionName)]
public class SalesUserHistoryTests
{
    private readonly ApiFixture _fx;
    public SalesUserHistoryTests(ApiFixture fx) { _fx = fx; }

    private const string Base = "/api/admin/sales-users";
    private static string Uniq(string prefix) => (prefix + Guid.NewGuid().ToString("N"))[..16];

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    private async Task<(string token, string name)> AdminAsync()
    {
        var u = await TestData.CreateUserAsync(_fx.Db, isSuperAdmin: true);
        var login = await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password);
        var name = await _fx.Db.ExecuteScalarAsync<string>("SELECT CONCAT(FirstName, N' ', LastName) FROM dbo.AdminUsers WHERE Username = @u", new[] { TestDb.P("@u", u.Username) });
        return (login.AccessToken!, name);
    }

    private async Task<(Guid id, string name)> CreateAsync(string token, string role, Guid? managerId = null)
    {
        string username = Uniq("hs");
        var res = await Call(HttpMethod.Post, Base, token, new
        {
            firstName = "Hist", lastName = username, username, email = username + "@sales.example.com", role, managerId,
            password = TestData.StrongPassword, confirmPassword = TestData.StrongPassword
        });
        res.Status.ShouldBe(201, res.Root.ToString());
        return (Guid.Parse(res.Data.GetProperty("id").GetString()!), "Hist " + username);
    }

    private static List<JsonElement> Entries(ApiResult r) => r.Data.EnumerateArray().ToList();

    [Fact]
    public async Task History_ShowsStatusAndManagerChanges_FromBothPanels_NewestFirst()
    {
        var (token, adminName) = await AdminAsync();
        var (managerA, nameA) = await CreateAsync(token, "SalesManager");
        var (managerB, nameB) = await CreateAsync(token, "SalesManager");
        var (person, _) = await CreateAsync(token, "SalesPerson", managerA);

        // Admin moves the sales person to manager B, then deactivates and activates them.
        var moved = await Call(HttpMethod.Put, $"{Base}/{person}", token, new
        {
            firstName = "Hist", lastName = "Moved", email = Uniq("hm") + "@sales.example.com", role = "SalesPerson", managerId = managerB
        });
        moved.Status.ShouldBe(200, moved.Root.ToString());
        (await Call(HttpMethod.Put, $"{Base}/{person}/status", token, new { status = "Inactive" })).Status.ShouldBe(200);
        (await Call(HttpMethod.Put, $"{Base}/{person}/status", token, new { status = "Active" })).Status.ShouldBe(200);

        // A manager deactivated them in the User Panel (row as the User API writes it).
        await _fx.Db.ExecuteNonQueryAsync(
            "INSERT dbo.AuditLogs (ActorUserId, Action, EntityType, EntityId, NewValues) VALUES (NULL, 'TeamMemberStatus', 'SalesUser', @e, @n)",
            new[] { TestDb.P("@e", person.ToString()), TestDb.P("@n", $"{{\"By\":\"SalesUser\",\"SalesUserId\":\"{managerB}\",\"SalesUserName\":\"{nameB}\",\"Values\":{{\"From\":\"Active\",\"To\":\"Inactive\"}}}}") });

        var one = await Call(HttpMethod.Get, $"{Base}/history?salesUserId={person}", token);
        one.Status.ShouldBe(200, one.Root.ToString());
        var e = Entries(one);
        e.Select(x => x.GetProperty("change").GetString()).ShouldBe(new[] { "Status", "Status", "Status", "Manager", "Details", "Created" });

        e[0].GetProperty("details").GetString().ShouldBe("Active → Inactive");
        e[0].GetProperty("byName").GetString().ShouldBe(nameB);
        e[0].GetProperty("byPanel").GetString().ShouldBe("User Panel");
        e[1].GetProperty("details").GetString().ShouldBe("Inactive → Active");
        e[2].GetProperty("details").GetString().ShouldBe("Active → Inactive");
        e[2].GetProperty("byName").GetString().ShouldBe(adminName);
        e[2].GetProperty("byPanel").GetString().ShouldBe("Admin Panel");
        e.Single(x => x.GetProperty("change").GetString() == "Manager").GetProperty("details").GetString().ShouldBe($"{nameA} → {nameB}");
        e.Last().GetProperty("details").GetString().ShouldBe($"Sales person, manager {nameA}");

        // Everyone's history includes this person, with their name; managers' own status changes appear too.
        (await Call(HttpMethod.Put, $"{Base}/{managerA}/status", token, new { status = "Inactive" })).Status.ShouldBe(200);
        var all = Entries(await Call(HttpMethod.Get, $"{Base}/history", token));
        all.ShouldContain(x => x.GetProperty("salesUserId").GetString() == managerA.ToString() && x.GetProperty("details").GetString() == "Active → Inactive");
        all.ShouldContain(x => x.GetProperty("salesUserId").GetString() == person.ToString() && x.GetProperty("change").GetString() == "Manager");
    }

    [Fact]
    public async Task History_NeedsSalesUserView()
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        await TestData.AssignGroupAsync(_fx.Db, u, "Product.View");
        var token = (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;
        (await Call(HttpMethod.Get, $"{Base}/history", token)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, $"{Base}/history", null)).Status.ShouldBe(401);
    }
}
