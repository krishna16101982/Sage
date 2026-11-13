using System.Text.Json;
using SageUserApi.Tests.Infrastructure;
using Shouldly;

namespace SageUserApi.Tests.IntegrationTests;

/// <summary>Sprint 9: catalog, customers, recording sales (server-side totals), my / team sales, cancelling.</summary>
[Collection(UserApiFixture.CollectionName)]
public class SalesApiTests
{
    private readonly UserApiFixture _fx;
    public SalesApiTests(UserApiFixture fx) { _fx = fx; }

    private const string Api = "/api/user";
    private Task<ApiResult> Call(HttpMethod m, string url, object? body = null, string? bearer = null)
        => UserHttp.SendAsync(_fx.CreateClient(), m, url, body, bearer);

    private async Task<string> TokenAsync(SalesUserRow u) => (await UserHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    /// <summary>Insert an active category > product > plan (price 1000.00, cost 600.00, 1 Year unless told otherwise).</summary>
    private async Task<(long categoryId, long productId, long planId)> PlanAsync(decimal price = 1000m, decimal cost = 600m, string unit = "Year", int count = 1, bool planActive = true)
    {
        var categoryId = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductCategories (Code, Name) OUTPUT INSERTED.Id VALUES (@c, @n)",
            new[] { TestDb.P("@c", Unique("Cat")), TestDb.P("@n", Unique("Category ")) });
        var productId = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.Products (CategoryId, Code, Name) OUTPUT INSERTED.Id VALUES (@cat, @c, @n)",
            new[] { TestDb.P("@cat", categoryId), TestDb.P("@c", Unique("Prd")), TestDb.P("@n", Unique("Product ")) });
        var planId = await _fx.Db.ExecuteScalarAsync<long>(
            "INSERT dbo.ProductPlans (ProductId, Name, DurationUnit, DurationCount, Price, Cost, IsActive) OUTPUT INSERTED.Id VALUES (@p, @n, @u, @dc, @pr, @co, @a)",
            new[] { TestDb.P("@p", productId), TestDb.P("@n", "Yearly"), TestDb.P("@u", unit), TestDb.P("@dc", count), TestDb.P("@pr", price), TestDb.P("@co", cost), TestDb.P("@a", planActive) });
        return (categoryId, productId, planId);
    }

    private async Task<Guid> CustomerAsync(string token, string? gstin = null)
    {
        var res = await Call(HttpMethod.Post, $"{Api}/customers", new { name = Unique("Acme "), city = "Pune", gstin }, token);
        res.Status.ShouldBe(201, res.Root.ToString());
        return res.Data.GetProperty("id").GetGuid();
    }

    private static object Sale(Guid customerId, long planId, int quantity = 1, decimal? saleAmount = null, decimal? discountAmount = null, string startDate = "")
        => new
        {
            customerId, planId, quantity, saleAmount, discountAmount,
            startDate = string.IsNullOrEmpty(startDate) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : startDate,
        };

    // ---------------- catalog & customers ----------------

    [Fact]
    public async Task Catalog_ListsOnlyActivePlans_WithPriceAndCost()
    {
        var token = await TokenAsync(await SalesData.CreateAsync(_fx.Db));
        var (_, productId, planId) = await PlanAsync(price: 499.50m, cost: 300m);
        var (_, hiddenProductId, _) = await PlanAsync(planActive: false);

        var res = await Call(HttpMethod.Get, $"{Api}/catalog", bearer: token);
        res.Status.ShouldBe(200);
        var products = res.Data.EnumerateArray().SelectMany(c => c.GetProperty("products").EnumerateArray()).ToList();
        var product = products.Single(p => p.GetProperty("id").GetInt64() == productId);
        var plan = product.GetProperty("plans").EnumerateArray().Single();
        plan.GetProperty("id").GetInt64().ShouldBe(planId);
        plan.GetProperty("price").GetDecimal().ShouldBe(499.50m);
        plan.GetProperty("cost").GetDecimal().ShouldBe(300m);
        products.ShouldNotContain(p => p.GetProperty("id").GetInt64() == hiddenProductId); // no active plan: not sellable
    }

    [Fact]
    public async Task Customers_Add_Search_AndRefuseDuplicates()
    {
        var token = await TokenAsync(await SalesData.CreateAsync(_fx.Db));
        var name = Unique("Globex ");
        var created = await Call(HttpMethod.Post, $"{Api}/customers",
            new { name, contactPerson = "Hank", email = "hank@globex.example.com", phone = "+91 98765 43210", gstin = "27aapfu0939f1zv", city = "Mumbai", state = "Maharashtra" }, token);
        created.Status.ShouldBe(201);
        created.Data.GetProperty("gstin").GetString().ShouldBe("27AAPFU0939F1ZV"); // stored upper-case
        created.Data.GetProperty("country").GetString().ShouldBe("India");

        var found = await Call(HttpMethod.Get, $"{Api}/customers?search={Uri.EscapeDataString(name)}", bearer: token);
        found.Data.GetProperty("totalCount").GetInt32().ShouldBe(1);

        (await Call(HttpMethod.Post, $"{Api}/customers", new { name = Unique("Other "), gstin = "27AAPFU0939F1ZV" }, token)).Status.ShouldBe(409);
        (await Call(HttpMethod.Post, $"{Api}/customers", new { name, city = "Mumbai" }, token)).Status.ShouldBe(409);
        (await Call(HttpMethod.Post, $"{Api}/customers", new { name, city = "Delhi" }, token)).Status.ShouldBe(201); // same name, other city

        var bad = await Call(HttpMethod.Post, $"{Api}/customers", new { name = "", gstin = "12345", email = "nope" }, token);
        bad.Status.ShouldBe(400);
        bad.Root.GetProperty("message").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task Endpoints_RequireSignIn()
    {
        (await Call(HttpMethod.Get, $"{Api}/catalog")).Status.ShouldBe(401);
        (await Call(HttpMethod.Get, $"{Api}/customers")).Status.ShouldBe(401);
        (await Call(HttpMethod.Get, $"{Api}/sales")).Status.ShouldBe(401);
    }

    // ---------------- recording a sale ----------------

    [Fact]
    public async Task CreateSale_WithSalePrice_TotalsComeFromPriceList()
    {
        var token = await TokenAsync(await SalesData.CreateAsync(_fx.Db));
        var (_, _, planId) = await PlanAsync(price: 1000m, cost: 600m);
        var customerId = await CustomerAsync(token);

        // Extra price/cost fields from the browser are ignored: the server uses the price list.
        var res = await Call(HttpMethod.Post, $"{Api}/sales",
            new { customerId, planId, quantity = 3, saleAmount = 2700m, unitPrice = 1m, unitCost = 0m, startDate = "2026-01-15" }, token);
        res.Status.ShouldBe(201, res.Root.ToString());
        var s = res.Data;
        s.GetProperty("saleNumber").GetString()!.ShouldStartWith("S-");
        s.GetProperty("unitPrice").GetDecimal().ShouldBe(1000m);
        s.GetProperty("listAmount").GetDecimal().ShouldBe(3000m);
        s.GetProperty("saleAmount").GetDecimal().ShouldBe(2700m);
        s.GetProperty("discountAmount").GetDecimal().ShouldBe(300m);
        s.GetProperty("discountPercent").GetDecimal().ShouldBe(10m);
        s.GetProperty("costAmount").GetDecimal().ShouldBe(1800m);
        s.GetProperty("margin").GetDecimal().ShouldBe(900m);
        s.GetProperty("renewalDate").GetString().ShouldBe("2027-01-15"); // start + plan duration (1 Year)
        s.GetProperty("status").GetString().ShouldBe("Active");
    }

    [Fact]
    public async Task CreateSale_WithDiscount_CalculatesSalePrice_AndHonoursDurationAndRenewal()
    {
        var token = await TokenAsync(await SalesData.CreateAsync(_fx.Db));
        var (_, _, planId) = await PlanAsync(price: 250m, cost: 100m, unit: "Month");
        var customerId = await CustomerAsync(token);

        var quarter = await Call(HttpMethod.Post, $"{Api}/sales",
            new { customerId, planId, quantity = 2, discountAmount = 50.25m, durationUnit = "Quarter", durationCount = 2, startDate = "2026-03-31" }, token);
        quarter.Status.ShouldBe(201, quarter.Root.ToString());
        quarter.Data.GetProperty("saleAmount").GetDecimal().ShouldBe(449.75m);
        quarter.Data.GetProperty("renewalDate").GetString().ShouldBe("2026-09-30"); // 6 months later (end of month kept)

        var custom = await Call(HttpMethod.Post, $"{Api}/sales",
            new { customerId, planId, quantity = 1, saleAmount = 250m, startDate = "2026-03-01", renewalDate = "2026-04-15" }, token);
        custom.Data.GetProperty("renewalDate").GetString().ShouldBe("2026-04-15");
        custom.Data.GetProperty("discountAmount").GetDecimal().ShouldBe(0m);
    }

    [Fact]
    public async Task CreateSale_RejectsInvalidAmountsAndUnavailablePlans()
    {
        var token = await TokenAsync(await SalesData.CreateAsync(_fx.Db));
        var (_, _, planId) = await PlanAsync(price: 100m, cost: 50m);
        var (_, _, inactivePlanId) = await PlanAsync(planActive: false);
        var customerId = await CustomerAsync(token);

        async Task Expect(object body, int status, string message)
        {
            var res = await Call(HttpMethod.Post, $"{Api}/sales", body, token);
            res.Status.ShouldBe(status, res.Root.ToString());
            res.FirstMessage.ShouldContain(message);
        }

        await Expect(Sale(customerId, planId, 2, saleAmount: 200.01m), 400, "more than the list amount");
        await Expect(Sale(customerId, planId, 1, discountAmount: 100.01m), 400, "more than the list amount");
        await Expect(Sale(customerId, planId, 1, saleAmount: -1m), 400, "negative");
        await Expect(Sale(customerId, planId, 1, saleAmount: 50m, discountAmount: 50m), 400, "either the sale price or the discount");
        await Expect(Sale(customerId, planId, 1), 400, "either the sale price or the discount");
        await Expect(Sale(customerId, planId, 1, saleAmount: 10.005m), 400, "2 decimal places");
        await Expect(Sale(customerId, planId, 0, saleAmount: 0m), 400, "Quantity");
        await Expect(Sale(customerId, inactivePlanId, 1, saleAmount: 1m), 400, "not available for sale");
        await Expect(Sale(customerId, 999_999_999, 1, saleAmount: 1m), 404, "Plan not found");
        await Expect(Sale(Guid.NewGuid(), planId, 1, saleAmount: 1m), 404, "Customer not found");
        await Expect(Sale(customerId, planId, 1, saleAmount: 1m, startDate: "2020-01-01"), 400, "within one year");
        await Expect(new { customerId, planId, quantity = 1, saleAmount = 1m, startDate = "2026-03-01", renewalDate = "2026-03-01" }, 400, "after the start date");
    }

    // ---------------- visibility & cancelling ----------------

    [Fact]
    public async Task MySales_TeamSales_AndCancelRules()
    {
        var manager = await SalesData.CreateAsync(_fx.Db);
        var alice = await SalesData.CreateAsync(_fx.Db, "SalesPerson", manager);
        var bob = await SalesData.CreateAsync(_fx.Db, "SalesPerson", manager);
        var outsider = await SalesData.CreateAsync(_fx.Db, "SalesPerson");
        string mt = await TokenAsync(manager), at = await TokenAsync(alice), bt = await TokenAsync(bob), ot = await TokenAsync(outsider);
        var (_, _, planId) = await PlanAsync(price: 1000m, cost: 400m);
        var customerId = await CustomerAsync(at);

        var a1 = (await Call(HttpMethod.Post, $"{Api}/sales", Sale(customerId, planId, 1, saleAmount: 900m), at)).Data.GetProperty("id").GetGuid();
        var a2 = (await Call(HttpMethod.Post, $"{Api}/sales", Sale(customerId, planId, 1, saleAmount: 800m), at)).Data.GetProperty("id").GetGuid();
        await Call(HttpMethod.Post, $"{Api}/sales", Sale(customerId, planId, 1, saleAmount: 1000m), bt);
        await Call(HttpMethod.Post, $"{Api}/sales", Sale(customerId, planId, 1, saleAmount: 700m), mt); // the manager's own sale

        // My sales
        var mine = await Call(HttpMethod.Get, $"{Api}/sales", bearer: at);
        mine.Data.GetProperty("totalCount").GetInt32().ShouldBe(2);
        mine.Data.GetProperty("totalSaleAmount").GetDecimal().ShouldBe(1700m);
        mine.Data.GetProperty("totalMargin").GetDecimal().ShouldBe(900m);
        (await Call(HttpMethod.Get, $"{Api}/sales", bearer: mt)).Data.GetProperty("totalCount").GetInt32().ShouldBe(1);

        // Team sales: managers only; the team's sales (not the manager's own), filterable by seller.
        (await Call(HttpMethod.Get, $"{Api}/sales?scope=team", bearer: at)).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, $"{Api}/sales/team-members", bearer: at)).Status.ShouldBe(403);
        var team = await Call(HttpMethod.Get, $"{Api}/sales?scope=team", bearer: mt);
        team.Data.GetProperty("totalCount").GetInt32().ShouldBe(3);
        (await Call(HttpMethod.Get, $"{Api}/sales?scope=team&sellerId={alice.PublicId}", bearer: mt)).Data.GetProperty("totalCount").GetInt32().ShouldBe(2);
        var members = await Call(HttpMethod.Get, $"{Api}/sales/team-members", bearer: mt);
        members.Data.GetArrayLength().ShouldBe(2);

        // Reading one sale: seller and manager yes; colleague and outsider no.
        (await Call(HttpMethod.Get, $"{Api}/sales/{a1}", bearer: at)).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, $"{Api}/sales/{a1}", bearer: mt)).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, $"{Api}/sales/{a1}", bearer: bt)).Status.ShouldBe(404);
        (await Call(HttpMethod.Get, $"{Api}/sales/{a1}", bearer: ot)).Status.ShouldBe(404);

        // Cancelling: reason required; colleague cannot; seller and manager can; only once.
        (await Call(HttpMethod.Post, $"{Api}/sales/{a1}/cancel", new { reason = " " }, at)).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Api}/sales/{a1}/cancel", new { reason = "Duplicate" }, bt)).Status.ShouldBe(404);
        var cancelled = await Call(HttpMethod.Post, $"{Api}/sales/{a1}/cancel", new { reason = "Customer changed their mind" }, mt);
        cancelled.Status.ShouldBe(200);
        cancelled.Data.GetProperty("status").GetString().ShouldBe("Cancelled");
        cancelled.Data.GetProperty("cancelReason").GetString().ShouldBe("Customer changed their mind");
        (await Call(HttpMethod.Post, $"{Api}/sales/{a1}/cancel", new { reason = "Again" }, at)).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Api}/sales/{a2}/cancel", new { reason = "Wrong plan" }, at)).Status.ShouldBe(200);

        // Cancelled sales stay in the list but are not counted in the totals.
        var after = await Call(HttpMethod.Get, $"{Api}/sales", bearer: at);
        after.Data.GetProperty("totalCount").GetInt32().ShouldBe(2);
        after.Data.GetProperty("totalSaleAmount").GetDecimal().ShouldBe(0m);
        (await Call(HttpMethod.Get, $"{Api}/sales?status=Active", bearer: at)).Data.GetProperty("totalCount").GetInt32().ShouldBe(0);

        // The customer list shows how many active sales a customer has (all sellers).
        var customer = await Call(HttpMethod.Get, $"{Api}/customers/{customerId}", bearer: bt);
        customer.Data.GetProperty("saleCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task TeamSales_StayWithTheManagerAtTheTimeOfSale()
    {
        var oldManager = await SalesData.CreateAsync(_fx.Db);
        var newManager = await SalesData.CreateAsync(_fx.Db);
        var person = await SalesData.CreateAsync(_fx.Db, "SalesPerson", oldManager);
        var pt = await TokenAsync(person);
        var (_, _, planId) = await PlanAsync();
        var customerId = await CustomerAsync(pt);
        await Call(HttpMethod.Post, $"{Api}/sales", Sale(customerId, planId, 1, saleAmount: 1000m), pt);

        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.SalesUsers SET ManagerId = @m WHERE Id = @id", new[] { TestDb.P("@m", newManager.Id), TestDb.P("@id", person.Id) });

        (await Call(HttpMethod.Get, $"{Api}/sales?scope=team", bearer: await TokenAsync(oldManager))).Data.GetProperty("totalCount").GetInt32().ShouldBe(1);
        (await Call(HttpMethod.Get, $"{Api}/sales?scope=team", bearer: await TokenAsync(newManager))).Data.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }
}
