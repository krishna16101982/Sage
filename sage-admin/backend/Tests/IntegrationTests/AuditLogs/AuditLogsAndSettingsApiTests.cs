using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.AuditLogs;

/// <summary>Audit log screen (read-only, filters, before/after values) and Settings screen (typed values with limits).</summary>
[Collection(ApiFixture.CollectionName)]
public class AuditLogsAndSettingsApiTests
{
    private readonly ApiFixture _fx;
    public AuditLogsAndSettingsApiTests(ApiFixture fx) { _fx = fx; }

    private async Task<(TestUser user, string token)> UserWithAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return (u, (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!);
    }

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    private Task SetSettingAsync(string key, string value)
        => _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.Settings SET SettingValue = @v WHERE SettingKey = @k", new[] { TestDb.P("@v", value), TestDb.P("@k", key) });

    [Fact]
    public async Task Endpoints_RequireTheirPermissions()
    {
        var (_, none) = await UserWithAsync("Customer.View");
        (await Call(HttpMethod.Get, "/api/admin/audit-logs", none)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, "/api/admin/audit-logs/options", none)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, "/api/admin/settings", none)).Status.ShouldBe(403);

        var (_, viewer) = await UserWithAsync("Setting.View", "AuditLog.View");
        (await Call(HttpMethod.Get, "/api/admin/audit-logs", viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, "/api/admin/settings", viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Put, "/api/admin/settings/Sales.MaxDiscountPercent", viewer, new { value = "50" })).Status.ShouldBe(403);
    }

    [Fact]
    public async Task Settings_AreTyped_Limited_Saved_AndAudited()
    {
        var (admin, token) = await UserWithAsync("Setting.View", "Setting.Edit", "AuditLog.View");
        try
        {
            var list = await Call(HttpMethod.Get, "/api/admin/settings", token);
            var keys = list.Data.EnumerateArray().Select(s => s.GetProperty("key").GetString()).ToList();
            keys.ShouldContain("Sales.MaxDiscountPercent");
            keys.ShouldContain("Sales.SaleNumberPrefix");
            keys.ShouldContain("Sales.MaxBackdateDays");
            var discount = list.Data.EnumerateArray().Single(s => s.GetProperty("key").GetString() == "Sales.MaxDiscountPercent");
            discount.GetProperty("valueType").GetString().ShouldBe("int");
            discount.GetProperty("maxValue").GetInt32().ShouldBe(100);

            // Wrong type, out of range, wrong format, unknown key.
            (await Call(HttpMethod.Put, "/api/admin/settings/Sales.MaxDiscountPercent", token, new { value = "abc" })).Status.ShouldBe(400);
            (await Call(HttpMethod.Put, "/api/admin/settings/Sales.MaxDiscountPercent", token, new { value = "150" })).Status.ShouldBe(400);
            (await Call(HttpMethod.Put, "/api/admin/settings/Sales.SaleNumberPrefix", token, new { value = "s1" })).Status.ShouldBe(400);
            (await Call(HttpMethod.Put, "/api/admin/settings/Sales.SaleNumberPrefix", token, new { value = "TOOLONG" })).Status.ShouldBe(400);
            (await Call(HttpMethod.Put, "/api/admin/settings/No.Such.Key", token, new { value = "1" })).Status.ShouldBe(404);

            var saved = await Call(HttpMethod.Put, "/api/admin/settings/Sales.MaxDiscountPercent", token, new { value = " 25 " });
            saved.Status.ShouldBe(200, saved.Root.ToString());
            saved.Data.GetProperty("value").GetString().ShouldBe("25");
            saved.Data.GetProperty("updatedByName").GetString().ShouldBe("Test User");

            // The change is in the audit log with the old and new value, and the log can be filtered to it.
            var log = await Call(HttpMethod.Get, $"/api/admin/audit-logs?entityType=Setting&actorId={admin.PublicId}", token);
            log.Status.ShouldBe(200);
            var entry = log.Data.GetProperty("items")[0];
            entry.GetProperty("entityId").GetString().ShouldBe("Sales.MaxDiscountPercent");
            entry.GetProperty("actorType").GetString().ShouldBe("admin");
            entry.GetProperty("actorName").GetString().ShouldBe("Test User");
            var detail = await Call(HttpMethod.Get, $"/api/admin/audit-logs/{entry.GetProperty("id").GetInt64()}", token);
            detail.Data.GetProperty("oldValues").GetProperty("Value").GetString().ShouldBe("100");
            detail.Data.GetProperty("newValues").GetProperty("Value").GetString().ShouldBe("25");
        }
        finally
        {
            await SetSettingAsync("Sales.MaxDiscountPercent", "100");
        }
    }

    [Fact]
    public async Task AuditLog_FiltersBySalesUserActor_Search_Dates_AndOptions()
    {
        var (_, token) = await UserWithAsync("AuditLog.View");
        var marker = "Z" + Guid.NewGuid().ToString("N")[..10];
        await _fx.Db.ExecuteNonQueryAsync(
            "INSERT dbo.AuditLogs (ActorUserId, Action, EntityType, EntityId, NewValues, IpAddress) VALUES (NULL, 'CancelRequest', 'Sale', @e, @n, '10.0.0.9')",
            new[] { TestDb.P("@e", marker), TestDb.P("@n", "{\"By\":\"SalesUser\",\"SalesUserName\":\"Priya Sharma\",\"Values\":{\"Reason\":\"Duplicate\"}}") });

        var bySales = await Call(HttpMethod.Get, $"/api/admin/audit-logs?actorType=sales&search={marker}", token);
        bySales.Data.GetProperty("totalCount").GetInt32().ShouldBe(1);
        var row = bySales.Data.GetProperty("items")[0];
        row.GetProperty("actorName").GetString().ShouldBe("Priya Sharma");
        row.GetProperty("action").GetString().ShouldBe("CancelRequest");
        (await Call(HttpMethod.Get, $"/api/admin/audit-logs?actorType=admin&search={marker}", token)).Data.GetProperty("totalCount").GetInt32().ShouldBe(0);
        (await Call(HttpMethod.Get, "/api/admin/audit-logs?search=Priya%20Sharma", token)).Data.GetProperty("totalCount").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        (await Call(HttpMethod.Get, $"/api/admin/audit-logs?search={marker}&from={today}&to={today}", token)).Data.GetProperty("totalCount").GetInt32().ShouldBe(1);
        (await Call(HttpMethod.Get, $"/api/admin/audit-logs?search={marker}&to=2000-01-01", token)).Data.GetProperty("totalCount").GetInt32().ShouldBe(0);

        var options = await Call(HttpMethod.Get, "/api/admin/audit-logs/options", token);
        options.Data.GetProperty("entityTypes").EnumerateArray().Select(x => x.GetString()).ShouldContain("Sale");
        options.Data.GetProperty("actions").EnumerateArray().Select(x => x.GetString()).ShouldContain("CancelRequest");
        (await Call(HttpMethod.Get, "/api/admin/audit-logs/999999999", token)).Status.ShouldBe(404);
    }
}
