using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Sales;

/// <summary>Admin Panel customers (add / edit, shared with the User Panel) and sales reporting (by seller, manager-wise, summary).</summary>
[Collection(ApiFixture.CollectionName)]
public class CustomersAndSalesApiTests
{
    private readonly ApiFixture _fx;
    public CustomersAndSalesApiTests(ApiFixture fx) { _fx = fx; }

    private static string Uniq(string prefix) => (prefix + Guid.NewGuid().ToString("N"))[..16];

    private async Task<string> TokenAsync(TestUser u) => (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;

    private async Task<string> UserTokenAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return await TokenAsync(u);
    }

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    // ---------------- data helpers (sales are recorded in the User Panel; here they are inserted directly) ----------------

    private record Seller(long Id, Guid PublicId);

    private async Task<Seller> SellerAsync(string role = "SalesManager", Seller? manager = null)
    {
        var u = Uniq("s");
        var id = await _fx.Db.ExecuteScalarAsync<long>(
            @"INSERT dbo.SalesUsers (FirstName, LastName, Username, Email, PasswordHash, MustChangePassword, Role, ManagerId)
              OUTPUT INSERTED.Id VALUES (N'Sam', @u, @u, @e, N'x', 0, @r, @m)",
            new[] { TestDb.P("@u", u), TestDb.P("@e", u + "@sales.example.com"), TestDb.P("@r", role), TestDb.P("@m", manager?.Id) });
        return new Seller(id, await _fx.Db.ExecuteScalarAsync<Guid>("SELECT PublicId FROM dbo.SalesUsers WHERE Id = @id", new[] { TestDb.P("@id", id) }));
    }

    private async Task<long> PlanAsync(decimal price = 1000m, decimal cost = 400m)
    {
        var cat = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductCategories (Code, Name) OUTPUT INSERTED.Id VALUES (@c, @c)", new[] { TestDb.P("@c", Uniq("C")) });
        var prod = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.Products (CategoryId, Code, Name) OUTPUT INSERTED.Id VALUES (@cat, @c, @c)",
            new[] { TestDb.P("@cat", cat), TestDb.P("@c", Uniq("P")) });
        return await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductPlans (ProductId, Name, Price, Cost) OUTPUT INSERTED.Id VALUES (@p, N'Yearly', @pr, @co)",
            new[] { TestDb.P("@p", prod), TestDb.P("@pr", price), TestDb.P("@co", cost) });
    }

    private async Task<Guid> CustomerRowAsync()
    {
        var id = Guid.NewGuid();
        await _fx.Db.ExecuteNonQueryAsync("INSERT dbo.Customers (PublicId, Name, City) VALUES (@id, @n, N'Pune')", new[] { TestDb.P("@id", id), TestDb.P("@n", Uniq("Cust ")) });
        return id;
    }

    private Task<Guid> SaleAsync(Seller seller, Guid customer, long plan, decimal amount)
        => _fx.Db.ExecuteProcedureScalarAsync<Guid>("dbo.usp_Sale_Create", new[]
        {
            TestDb.P("@SalesUserId", seller.Id), TestDb.P("@CustomerPublicId", customer), TestDb.P("@PlanId", plan), TestDb.P("@Quantity", 1),
            TestDb.P("@SaleAmount", amount), TestDb.P("@StartDate", DateTime.UtcNow.Date)
        });

    // ---------------- customers ----------------

    [Fact]
    public async Task Customers_RequireTheirPermissions()
    {
        var none = await UserTokenAsync();
        (await Call(HttpMethod.Get, "/api/admin/customers", none)).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, "/api/admin/customers", none, new { name = "x" })).Status.ShouldBe(403);

        var viewer = await UserTokenAsync("Customer.View");
        (await Call(HttpMethod.Get, "/api/admin/customers", viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Post, "/api/admin/customers", viewer, new { name = "x" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"/api/admin/customers/{Guid.NewGuid()}", viewer, new { name = "x" })).Status.ShouldBe(403);
    }

    [Fact]
    public async Task Customers_AddEditSearch_AndRefuseDuplicates()
    {
        var token = await UserTokenAsync("Customer.View", "Customer.Add", "Customer.Edit");
        var name = Uniq("Initech ");
        var created = await Call(HttpMethod.Post, "/api/admin/customers", token, new { name, gstin = "29aabcu9603r1zm", city = "Bengaluru", phone = "+91 80 1234 5678" });
        created.Status.ShouldBe(201, created.Root.ToString());
        created.Data.GetProperty("gstin").GetString().ShouldBe("29AABCU9603R1ZM");
        created.Data.GetProperty("createdByName").GetString()!.ShouldEndWith("(Admin)");
        var id = created.Data.GetProperty("id").GetGuid();

        (await Call(HttpMethod.Get, $"/api/admin/customers?search={Uri.EscapeDataString(name)}", token)).Data.GetProperty("totalCount").GetInt32().ShouldBe(1);
        (await Call(HttpMethod.Post, "/api/admin/customers", token, new { name = Uniq("Other "), gstin = "29AABCU9603R1ZM" })).Status.ShouldBe(409);
        var bad = await Call(HttpMethod.Post, "/api/admin/customers", token, new { name = " ", email = "no", gstin = "X" });
        bad.Status.ShouldBe(400);
        bad.Root.GetProperty("message").GetArrayLength().ShouldBe(3);

        var edited = await Call(HttpMethod.Put, $"/api/admin/customers/{id}", token, new { name, gstin = "29AABCU9603R1ZM", city = "Mysuru", contactPerson = "Bill" });
        edited.Status.ShouldBe(200, edited.Root.ToString());
        edited.Data.GetProperty("city").GetString().ShouldBe("Mysuru");
        edited.Data.GetProperty("contactPerson").GetString().ShouldBe("Bill");

        // Another customer cannot take the same name in the same city, nor the same GSTIN.
        var other = (await Call(HttpMethod.Post, "/api/admin/customers", token, new { name = Uniq("Second "), city = "Mysuru" })).Data.GetProperty("id").GetGuid();
        (await Call(HttpMethod.Put, $"/api/admin/customers/{other}", token, new { name, city = "Mysuru" })).Status.ShouldBe(409);
        (await Call(HttpMethod.Put, $"/api/admin/customers/{other}", token, new { name = Uniq("Second "), gstin = "29AABCU9603R1ZM" })).Status.ShouldBe(409);
        (await Call(HttpMethod.Put, $"/api/admin/customers/{Guid.NewGuid()}", token, new { name = "x" })).Status.ShouldBe(404);

        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType = 'Customer' AND EntityId = @id", new[] { TestDb.P("@id", id.ToString()) })).ShouldBe(2);
    }

    // ---------------- sales ----------------

    [Fact]
    public async Task Sales_RequireSalesView()
    {
        var none = await UserTokenAsync("Customer.View");
        (await Call(HttpMethod.Get, "/api/admin/sales", none)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, "/api/admin/sales/summary", none)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, "/api/admin/sales/sellers", none)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, "/api/admin/sales", await UserTokenAsync("Sales.View"))).Status.ShouldBe(200);
    }

    [Fact]
    public async Task Sales_BySeller_ManagerWise_Summary_AndDetail()
    {
        var token = await UserTokenAsync("Sales.View");
        var m = await SellerAsync();
        var a = await SellerAsync("SalesPerson", m);
        var b = await SellerAsync("SalesPerson", m);
        var n = await SellerAsync();
        var c = await SellerAsync("SalesPerson", n);
        var plan = await PlanAsync(price: 1000m, cost: 400m);
        var customer = await CustomerRowAsync();

        var a1 = await SaleAsync(a, customer, plan, 900m);
        var a2 = await SaleAsync(a, customer, plan, 800m);
        await SaleAsync(b, customer, plan, 1000m);
        await SaleAsync(m, customer, plan, 700m);      // the manager's own sale
        await SaleAsync(c, customer, plan, 500m);
        await _fx.Db.ExecuteProcedureScalarAsync<int>("dbo.usp_Sale_Cancel", new[] { TestDb.P("@SalePublicId", a2), TestDb.P("@SalesUserId", a.Id), TestDb.P("@Reason", "test") });

        // One sales person
        var bySeller = await Call(HttpMethod.Get, $"/api/admin/sales?sellerId={a.PublicId}", token);
        bySeller.Data.GetProperty("totalCount").GetInt32().ShouldBe(2);
        bySeller.Data.GetProperty("totalSaleAmount").GetDecimal().ShouldBe(900m);   // the cancelled sale is not counted
        bySeller.Data.GetProperty("totalMargin").GetDecimal().ShouldBe(500m);

        // Manager-wise: the manager's own sale plus the team's
        var byManager = await Call(HttpMethod.Get, $"/api/admin/sales?managerId={m.PublicId}&status=Active", token);
        byManager.Data.GetProperty("totalCount").GetInt32().ShouldBe(3);
        byManager.Data.GetProperty("totalSaleAmount").GetDecimal().ShouldBe(2600m);
        var rows = byManager.Data.GetProperty("items").EnumerateArray().ToList();
        rows.Single(r => r.GetProperty("sellerId").GetGuid() == a.PublicId).GetProperty("managerName").GetString().ShouldNotBeNull();
        rows.Single(r => r.GetProperty("sellerId").GetGuid() == m.PublicId).GetProperty("sellerRole").GetString().ShouldBe("SalesManager");
        (await Call(HttpMethod.Get, $"/api/admin/sales?managerId={n.PublicId}", token)).Data.GetProperty("totalCount").GetInt32().ShouldBe(1);

        // Summary per sales person
        var sellers = (await Call(HttpMethod.Get, "/api/admin/sales/summary?groupBy=seller", token)).Data.EnumerateArray().ToList();
        var rowA = sellers.Single(r => r.GetProperty("id").GetGuid() == a.PublicId);
        rowA.GetProperty("saleCount").GetInt32().ShouldBe(1);
        rowA.GetProperty("cancelledCount").GetInt32().ShouldBe(1);
        rowA.GetProperty("saleAmount").GetDecimal().ShouldBe(900m);
        rowA.GetProperty("margin").GetDecimal().ShouldBe(500m);
        sellers.Single(r => r.GetProperty("id").GetGuid() == m.PublicId).GetProperty("saleAmount").GetDecimal().ShouldBe(700m);

        // Summary per manager (own + team), sales persons are not rows of their own
        var managers = (await Call(HttpMethod.Get, "/api/admin/sales/summary?groupBy=manager", token)).Data.EnumerateArray().ToList();
        var rowM = managers.Single(r => r.GetProperty("id").GetGuid() == m.PublicId);
        rowM.GetProperty("saleCount").GetInt32().ShouldBe(3);
        rowM.GetProperty("cancelledCount").GetInt32().ShouldBe(1);
        rowM.GetProperty("saleAmount").GetDecimal().ShouldBe(2600m);
        rowM.GetProperty("teamSize").GetInt32().ShouldBe(2);
        managers.Single(r => r.GetProperty("id").GetGuid() == n.PublicId).GetProperty("saleAmount").GetDecimal().ShouldBe(500m);
        managers.ShouldNotContain(r => r.GetProperty("id").GetGuid() == a.PublicId);

        // Detail, sellers list
        var detail = await Call(HttpMethod.Get, $"/api/admin/sales/{a1}", token);
        detail.Status.ShouldBe(200);
        detail.Data.GetProperty("saleAmount").GetDecimal().ShouldBe(900m);
        detail.Data.GetProperty("discountPercent").GetDecimal().ShouldBe(10m);
        (await Call(HttpMethod.Get, $"/api/admin/sales/{Guid.NewGuid()}", token)).Status.ShouldBe(404);
        var options = (await Call(HttpMethod.Get, "/api/admin/sales/sellers", token)).Data.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        options.ShouldContain(a.PublicId);
        options.ShouldContain(m.PublicId);
    }
}
