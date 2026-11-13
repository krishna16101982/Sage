using System.Net.Http.Headers;
using System.Text;
using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Sales;

/// <summary>CSV exports of the Admin Sales list and summary: permission, filters, Excel-safe content, audit.</summary>
[Collection(ApiFixture.CollectionName)]
public class SalesExportApiTests
{
    private readonly ApiFixture _fx;
    public SalesExportApiTests(ApiFixture fx) { _fx = fx; }

    private static string Uniq(string prefix) => (prefix + Guid.NewGuid().ToString("N"))[..16];

    private async Task<(TestUser user, string token)> UserWithAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return (u, (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!);
    }

    private async Task<(HttpResponseMessage res, byte[] bytes, string text)> GetFileAsync(string url, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _fx.CreateClient().SendAsync(req);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        return (res, bytes, Encoding.UTF8.GetString(bytes).TrimStart('﻿'));
    }

    private async Task<(long Id, Guid PublicId, string Username)> SellerAsync(string role = "SalesManager", long? managerId = null)
    {
        var u = Uniq("s");
        var id = await _fx.Db.ExecuteScalarAsync<long>(
            @"INSERT dbo.SalesUsers (FirstName, LastName, Username, Email, PasswordHash, MustChangePassword, Role, ManagerId)
              OUTPUT INSERTED.Id VALUES (N'Sam', @u, @u, @e, N'x', 0, @r, @m)",
            new[] { TestDb.P("@u", u), TestDb.P("@e", u + "@sales.example.com"), TestDb.P("@r", role), TestDb.P("@m", managerId) });
        return (id, await _fx.Db.ExecuteScalarAsync<Guid>("SELECT PublicId FROM dbo.SalesUsers WHERE Id = @id", new[] { TestDb.P("@id", id) }), u);
    }

    private async Task<long> PlanAsync()
    {
        var cat = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductCategories (Code, Name) OUTPUT INSERTED.Id VALUES (@c, @c)", new[] { TestDb.P("@c", Uniq("C")) });
        var prod = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.Products (CategoryId, Code, Name) OUTPUT INSERTED.Id VALUES (@cat, @c, @c)",
            new[] { TestDb.P("@cat", cat), TestDb.P("@c", Uniq("P")) });
        return await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductPlans (ProductId, Name, Price, Cost) OUTPUT INSERTED.Id VALUES (@p, N'Yearly', 1000, 400)",
            new[] { TestDb.P("@p", prod) });
    }

    private async Task<Guid> CustomerAsync(string name)
    {
        var id = Guid.NewGuid();
        await _fx.Db.ExecuteNonQueryAsync("INSERT dbo.Customers (PublicId, Name, City) VALUES (@id, @n, N'Pune')", new[] { TestDb.P("@id", id), TestDb.P("@n", name) });
        return id;
    }

    private Task<Guid> SaleAsync(long sellerId, Guid customer, long plan, decimal amount, string? notes = null)
        => _fx.Db.ExecuteProcedureScalarAsync<Guid>("dbo.usp_Sale_Create", new[]
        {
            TestDb.P("@SalesUserId", sellerId), TestDb.P("@CustomerPublicId", customer), TestDb.P("@PlanId", plan), TestDb.P("@Quantity", 1),
            TestDb.P("@SaleAmount", amount), TestDb.P("@StartDate", DateTime.UtcNow.Date), TestDb.P("@Notes", notes)
        });

    [Fact]
    public async Task Exports_RequireSalesView()
    {
        var (_, none) = await UserWithAsync("Customer.View");
        (await GetFileAsync("/api/admin/sales/export", none)).res.StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
        (await GetFileAsync("/api/admin/sales/summary/export", none)).res.StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SalesExport_FollowsFilters_IsExcelSafe_AndAudited()
    {
        var (admin, token) = await UserWithAsync("Sales.View", "AuditLog.View");
        var seller = await SellerAsync();
        var plan = await PlanAsync();
        // A customer name that a spreadsheet would run as a formula, and notes with a comma, quote and line break.
        var risky = await CustomerAsync("=HYPERLINK(\"http://x\")" + Uniq(""));
        await SaleAsync(seller.Id, risky, plan, 900m, "first, \"second\"\nthird");
        await SaleAsync(seller.Id, await CustomerAsync(Uniq("Plain ")), plan, 1000m);
        await SaleAsync((await SellerAsync()).Id, risky, plan, 500m);   // another seller: not in the export

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var (res, bytes, text) = await GetFileAsync($"/api/admin/sales/export?sellerId={seller.PublicId}&soldFrom={today}&soldTo={today}", token);
        res.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, text);
        res.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        res.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe($"sales_{today}_to_{today}.csv");
        res.Headers.CacheControl!.NoStore.ShouldBeTrue();
        bytes.Take(3).ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });   // UTF-8 BOM so Excel reads ₹ and names correctly

        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].ShouldStartWith("Sale number,Sold at (UTC),Customer,");
        text.ShouldContain("\"'=HYPERLINK(\"\"http://x\"\")");          // prefixed with ' and quoted
        text.ShouldContain("\"first, \"\"second\"\"\nthird\"");          // comma, quotes and line break kept inside one field
        text.ShouldContain(",1000.00,100.00,10.00,900.00,400.00,500.00,1 Year,");
        res.Headers.GetValues("X-Row-Count").Single().ShouldBe("2");

        // The export is in the audit log with the filters used and the row count.
        var log = await AuthHttp.SendAsync(_fx.CreateClient(), HttpMethod.Get, $"/api/admin/audit-logs?action=Export&actorId={admin.PublicId}", null, token);
        var entry = log.Data.GetProperty("items")[0];
        entry.GetProperty("entityType").GetString().ShouldBe("Sale");
        var detail = await AuthHttp.SendAsync(_fx.CreateClient(), HttpMethod.Get, $"/api/admin/audit-logs/{entry.GetProperty("id").GetInt64()}", null, token);
        detail.Data.GetProperty("newValues").GetProperty("Report").GetString().ShouldBe("Sales");
        detail.Data.GetProperty("newValues").GetProperty("Rows").GetInt32().ShouldBe(2);
        detail.Data.GetProperty("newValues").GetProperty("SellerId").GetGuid().ShouldBe(seller.PublicId);
    }

    [Fact]
    public async Task SummaryExport_PerPersonAndPerManager_WithTotalRow()
    {
        var (_, token) = await UserWithAsync("Sales.View");
        var manager = await SellerAsync();
        var member = await SellerAsync("SalesPerson", manager.Id);
        var plan = await PlanAsync();
        var customer = await CustomerAsync(Uniq("Cust "));
        await SaleAsync(manager.Id, customer, plan, 700m);
        await SaleAsync(member.Id, customer, plan, 900m);

        var (res, _, text) = await GetFileAsync("/api/admin/sales/summary/export?groupBy=seller", token);
        res.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, text);
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].ShouldBe("Sales person,Username,Role,Manager,Sales,Cancelled,Quantity,List amount,Discount,Sale amount,Cost,Margin");
        lines.Single(l => l.Contains("," + member.Username + ",")).ShouldEndWith(",SalesPerson,Sam " + manager.Username + ",1,0,1,1000.00,100.00,900.00,400.00,500.00");
        lines[^1].ShouldStartWith("Total,,,,");

        var (byManager, _, managerText) = await GetFileAsync("/api/admin/sales/summary/export?groupBy=manager", token);
        byManager.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldStartWith("sales-by-manager_");
        var managerLines = managerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        managerLines[0].ShouldStartWith("Manager,Username,Team size,");
        // The manager's row covers their own sale and the team member's: 2 sales, 1600.00.
        managerLines.Single(l => l.Contains("," + manager.Username + ",")).ShouldEndWith(",1,2,0,2,2000.00,400.00,1600.00,800.00,800.00");
    }
}
