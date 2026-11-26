using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Sales;

/// <summary>Admin dashboard: lead figures for every lead or one sales user (GET api/admin/leads/summary, Sales.View).</summary>
[Collection(ApiFixture.CollectionName)]
public class LeadSummaryApiTests
{
    private readonly ApiFixture _fx;
    public LeadSummaryApiTests(ApiFixture fx) { _fx = fx; }

    private const string Url = "/api/admin/leads/summary";
    private static string Uniq(string p) => p + Guid.NewGuid().ToString("N")[..10];

    private async Task<string> TokenAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;
    }

    private Task<ApiResult> Get(string url, string? token) => AuthHttp.SendAsync(_fx.CreateClient(), HttpMethod.Get, url, null, token);

    private async Task<(long Id, Guid PublicId)> SellerAsync()
    {
        var u = Uniq("s");
        var id = await _fx.Db.ExecuteScalarAsync<long>(
            @"INSERT dbo.SalesUsers (FirstName, LastName, Username, Email, PasswordHash, MustChangePassword, Role)
              OUTPUT INSERTED.Id VALUES (N'Sam', @u, @u, @e, N'x', 0, 'SalesManager')",
            new[] { TestDb.P("@u", u), TestDb.P("@e", u + "@sales.example.com") });
        return (id, await _fx.Db.ExecuteScalarAsync<Guid>("SELECT PublicId FROM dbo.SalesUsers WHERE Id = @id", new[] { TestDb.P("@id", id) }));
    }

    private async Task LeadAsync(long seller, long company, string status, decimal? revenue, int daysAgo)
        => await _fx.Db.ExecuteNonQueryAsync(
            @"INSERT dbo.Leads (LeadNumber, CustomerId, StatusId, TypeId, GeneratedBySalesUserId, AssignedSalesUserId, LeadDate,
                                CreatedBySalesUserId, ProductId, MonthlyExpectedRevenue)
              VALUES (@n, @c, (SELECT Id FROM dbo.LeadStatuses WHERE Code = @st), 1, @s, @s,
                      DATEADD(DAY, -@d, CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+05:30') AS DATE)), @s,
                      (SELECT TOP 1 Id FROM dbo.LeadProducts ORDER BY Id), @r)",
            new[] { TestDb.P("@n", Uniq("T")[..11]), TestDb.P("@c", company), TestDb.P("@st", status), TestDb.P("@s", seller),
                    TestDb.P("@d", daysAgo), TestDb.P("@r", revenue) });

    [Fact]
    public async Task Summary_NeedsSalesView_AndCountsEveryLeadOrOneSalesUsers()
    {
        (await Get(Url, null)).Status.ShouldBe(401);
        (await Get(Url, await TokenAsync("Customer.View"))).Status.ShouldBe(403);
        var token = await TokenAsync("Sales.View");

        var seller = await SellerAsync();
        var company = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.Customers (PublicId, Name, City) OUTPUT INSERTED.Id VALUES (NEWID(), @n, N'Pune')",
            new[] { TestDb.P("@n", Uniq("Co ")) });
        await LeadAsync(seller.Id, company, "WON", 5000m, 2);
        await LeadAsync(seller.Id, company, "OPEN", 1000m, 40);
        await LeadAsync(seller.Id, company, "WON", 2500m, 90);
        await LeadAsync(seller.Id, company, "LOST", null, 10);

        var mine = await Get($"{Url}?salesUserId={seller.PublicId}", token);
        mine.Status.ShouldBe(200, mine.Root.ToString());
        var d = mine.Data;
        d.GetProperty("totalLeads").GetInt32().ShouldBe(4);
        d.GetProperty("wonLeads").GetInt32().ShouldBe(2);
        d.GetProperty("conversionRate").GetDecimal().ShouldBe(50m);
        d.GetProperty("wonRevenueMonthly").GetDecimal().ShouldBe(7500m);
        d.GetProperty("wonRevenueAnnualized").GetDecimal().ShouldBe(90000m);
        var statuses = d.GetProperty("statuses").EnumerateArray().ToDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("total").GetInt32());
        statuses.Count.ShouldBe(6);
        statuses["WON"].ShouldBe(2); statuses["OPEN"].ShouldBe(1); statuses["LOST"].ShouldBe(1); statuses["HOLD"].ShouldBe(0);
        d.GetProperty("aging").EnumerateArray().Select(x => x.GetProperty("total").GetInt32()).ShouldBe(new[] { 1, 1, 1, 1 });
        var products = d.GetProperty("products").EnumerateArray().ToList();
        products.Count.ShouldBe(1);
        products[0].GetProperty("won").GetInt32().ShouldBe(2);
        products[0].GetProperty("revenue").GetDecimal().ShouldBe(7500m);

        var all = await Get(Url, token);
        all.Status.ShouldBe(200, all.Root.ToString());
        all.Data.GetProperty("totalLeads").GetInt32().ShouldBeGreaterThanOrEqualTo(4);

        (await Get($"{Url}?salesUserId={Guid.NewGuid()}", token)).Status.ShouldBe(404);
    }
}
