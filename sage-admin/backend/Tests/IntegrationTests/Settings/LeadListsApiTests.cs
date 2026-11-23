using System.Text.Json;
using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Settings;

/// <summary>Lead lists (products, lead sources, billing cycles), one table each, managed under Settings.</summary>
[Collection(ApiFixture.CollectionName)]
public class LeadListsApiTests
{
    private readonly ApiFixture _fx;
    public LeadListsApiTests(ApiFixture fx) { _fx = fx; }

    private const string Base = "/api/admin/lead-lists";

    private async Task<string> TokenAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;
    }

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    private static JsonElement List(ApiResult lists, string list)
        => lists.Data.EnumerateArray().Single(l => l.GetProperty("list").GetString() == list);

    [Fact]
    public async Task Lists_NeedSettingView_ChangesNeedSettingEdit_AndHoldTheSeededValues()
    {
        (await Call(HttpMethod.Get, Base, await TokenAsync("Customer.View"))).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, Base, null)).Status.ShouldBe(401);
        var viewer = await TokenAsync("Setting.View");
        var lists = await Call(HttpMethod.Get, Base, viewer);
        lists.Status.ShouldBe(200, lists.Root.ToString());
        lists.Data.EnumerateArray().Select(l => l.GetProperty("list").GetString()).ShouldBe(new[] { "Product", "Source", "BillingCycle" });
        List(lists, "Product").GetProperty("values").EnumerateArray().Select(v => v.GetProperty("name").GetString()).ShouldContain("TALLY ON CLOUD");
        List(lists, "Source").GetProperty("values")[0].GetProperty("name").GetString().ShouldBe("Cold Call");
        // Seeded values (other tests may add more to the shared test database).
        var cycles = List(lists, "BillingCycle").GetProperty("values").EnumerateArray().Select(v => v.GetProperty("name").GetString()).ToList();
        foreach (var seeded in new[] { "Monthly", "Quarterly", "Half Yearly", "Yearly" }) cycles.ShouldContain(seeded);
        (await Call(HttpMethod.Post, Base, viewer, new { list = "Product", name = "SAP" })).Status.ShouldBe(403);
    }

    [Fact]
    public async Task Values_AreAdded_Renamed_TurnedOff_AndNamesStayUnique()
    {
        var token = await TokenAsync("Setting.View", "Setting.Edit");
        string name = "Src " + Guid.NewGuid().ToString("N")[..8];

        (await Call(HttpMethod.Post, Base, token, new { list = "Nope", name })).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, Base, token, new { list = "Source", name = "" })).Status.ShouldBe(400);

        var created = await Call(HttpMethod.Post, Base, token, new { list = "Source", name, sortOrder = 50 });
        created.Status.ShouldBe(201, created.Root.ToString());
        int id = created.Data.GetProperty("id").GetInt32();
        created.Data.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        (await Call(HttpMethod.Post, Base, token, new { list = "Source", name })).Status.ShouldBe(409);
        // The same name may exist in another list.
        (await Call(HttpMethod.Post, Base, token, new { list = "BillingCycle", name })).Status.ShouldBe(201);

        var renamed = await Call(HttpMethod.Put, $"{Base}/{id}", token, new { list = "Source", name = name + " 2", sortOrder = 51, isActive = false });
        renamed.Status.ShouldBe(200, renamed.Root.ToString());
        renamed.Data.GetProperty("name").GetString().ShouldBe(name + " 2");
        renamed.Data.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        renamed.Data.GetProperty("usedBy").GetInt32().ShouldBe(0);
        (await Call(HttpMethod.Put, $"{Base}/999999", token, new { list = "Source", name = "x" })).Status.ShouldBe(404);

        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType = 'LeadListValue' AND EntityId = @e",
            new[] { TestDb.P("@e", $"Source:{id}") })).ShouldBe(2);
    }
}
