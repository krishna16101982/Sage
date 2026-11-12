using System.Text.Json;
using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Products;

[Collection(ApiFixture.CollectionName)]
public class ProductsApiTests
{
    private readonly ApiFixture _fx;
    public ProductsApiTests(ApiFixture fx) { _fx = fx; }

    private const string Base = "/api/admin/products";
    private static string Uniq(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

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

    private async Task<long> CategoryAsync(string token, string? name = null)
    {
        var res = await Call(HttpMethod.Post, $"{Base}/categories", token, new { code = Uniq("C"), name = name ?? Uniq("Category "), sortOrder = 900 });
        res.Status.ShouldBe(201);
        return res.Data.GetProperty("id").GetInt64();
    }

    private async Task<long> ProductAsync(string token, long categoryId, string? name = null)
    {
        var res = await Call(HttpMethod.Post, Base, token, new { categoryId, code = Uniq("P"), name = name ?? Uniq("Product "), sortOrder = 1 });
        res.Status.ShouldBe(201);
        return res.Data.GetProperty("id").GetInt64();
    }

    private static object Plan(string name = "Yearly", decimal? price = 12000m, decimal? cost = 9000m, string unit = "Year", int count = 1)
        => new { name, durationUnit = unit, durationCount = count, price, cost, sortOrder = 1 };

    private static JsonElement? Find(JsonElement catalog, long categoryId, long? productId = null)
    {
        foreach (var c in catalog.EnumerateArray())
        {
            if (c.GetProperty("id").GetInt64() != categoryId) continue;
            if (productId == null) return c;
            foreach (var p in c.GetProperty("products").EnumerateArray())
                if (p.GetProperty("id").GetInt64() == productId) return p;
        }
        return null;
    }

    // ---------------- 401 / 403 ----------------

    [Theory]
    [InlineData("GET", Base + "/catalog")]
    [InlineData("POST", Base + "/categories")]
    [InlineData("PUT", Base + "/categories/1")]
    [InlineData("POST", Base)]
    [InlineData("PUT", Base + "/1")]
    [InlineData("POST", Base + "/1/plans")]
    [InlineData("PUT", Base + "/plans/1")]
    public async Task EveryEndpoint_WithoutToken_Returns401(string method, string url)
        => (await Call(new HttpMethod(method), url, null, method == "GET" ? null : new { })).Status.ShouldBe(401);

    [Fact]
    public async Task ViewOnly_CanRead_ButNotWrite_AndOthersGet403()
    {
        var none = await UserTokenAsync("Group.View", "Permission.View");
        (await Call(HttpMethod.Get, $"{Base}/catalog", none)).Status.ShouldBe(403);

        var viewer = await UserTokenAsync("Product.View");
        (await Call(HttpMethod.Get, $"{Base}/catalog", viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Post, $"{Base}/categories", viewer, new { code = Uniq("C"), name = "x" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/categories/1", viewer, new { name = "x" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, Base, viewer, new { categoryId = 1, code = Uniq("P"), name = "x" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/1", viewer, new { categoryId = 1, name = "x" })).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, $"{Base}/1/plans", viewer, Plan())).Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Base}/plans/1", viewer, Plan())).Status.ShouldBe(403);

        // Add and Edit are separate.
        var adder = await UserTokenAsync("Product.Add");
        (await Call(HttpMethod.Post, $"{Base}/categories", adder, new { code = Uniq("C"), name = Uniq("Cat ") })).Status.ShouldBe(201);
        (await Call(HttpMethod.Put, $"{Base}/categories/1", adder, new { name = "x" })).Status.ShouldBe(403);
    }

    // ---------------- seed ----------------

    [Fact]
    public async Task Seed_HasSaaSWithFourProducts_AndCyberSecurity()
    {
        var res = await Call(HttpMethod.Get, $"{Base}/catalog", await AdminTokenAsync());
        res.Status.ShouldBe(200);
        var categories = res.Data.EnumerateArray().ToDictionary(c => c.GetProperty("code").GetString()!);
        categories.ShouldContainKey("SaaS");
        categories.ShouldContainKey("CyberSecurity");
        categories["CyberSecurity"].GetProperty("name").GetString().ShouldBe("Cyber Security");
        var saas = categories["SaaS"].GetProperty("products").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToList();
        foreach (var code in new[] { "SHTMS", "GreytHR", "LinkedIn", "Freshdesk" }) saas.ShouldContain(code);
    }

    [Fact]
    public async Task Seed_ProductPermissionsExist_AndCanBeGranted()
        => (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Permissions WHERE Code IN ('Product.View','Product.Add','Product.Edit') AND IsActive = 1")).ShouldBe(3);

    // ---------------- categories ----------------

    [Fact]
    public async Task Category_CreateUpdate_DuplicatesAndAudit()
    {
        var token = await AdminTokenAsync();
        var code = Uniq("C");
        var name = Uniq("Network ");
        var created = await Call(HttpMethod.Post, $"{Base}/categories", token, new { code, name, description = " desc ", sortOrder = 5 });
        created.Status.ShouldBe(201);
        created.Data.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        created.Data.GetProperty("description").GetString().ShouldBe("desc");
        var id = created.Data.GetProperty("id").GetInt64();

        var dupCode = await Call(HttpMethod.Post, $"{Base}/categories", token, new { code, name = Uniq("Other ") });
        dupCode.Status.ShouldBe(409);
        dupCode.FirstMessage.ShouldContain("code");
        (await Call(HttpMethod.Post, $"{Base}/categories", token, new { code = Uniq("C"), name })).Status.ShouldBe(409);
        (await Call(HttpMethod.Post, $"{Base}/categories", token, new { code = "bad code", name = "x" })).Status.ShouldBe(400);

        var renamed = Uniq("Renamed ");
        var updated = await Call(HttpMethod.Put, $"{Base}/categories/{id}", token, new { name = renamed, sortOrder = 7, isActive = false });
        updated.Status.ShouldBe(200);
        updated.Data.GetProperty("name").GetString().ShouldBe(renamed);
        updated.Data.GetProperty("code").GetString().ShouldBe(code);
        updated.Data.GetProperty("isActive").GetBoolean().ShouldBeFalse();

        (await Call(HttpMethod.Put, $"{Base}/categories/999999999", token, new { name = "x", isActive = true })).Status.ShouldBe(404);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType = 'ProductCategory' AND EntityId = @id",
            new[] { TestDb.P("@id", id.ToString()) })).ShouldBe(2);
    }

    // ---------------- products ----------------

    [Fact]
    public async Task Product_CreateUpdate_MoveCategory_AndNameUniquePerCategory()
    {
        var token = await AdminTokenAsync();
        var catA = await CategoryAsync(token);
        var catB = await CategoryAsync(token);
        var productId = await ProductAsync(token, catA, "Antivirus");

        // Same name allowed in another category, not in the same one.
        (await Call(HttpMethod.Post, Base, token, new { categoryId = catB, code = Uniq("P"), name = "Antivirus" })).Status.ShouldBe(201);
        var dup = await Call(HttpMethod.Post, Base, token, new { categoryId = catA, code = Uniq("P"), name = "Antivirus" });
        dup.Status.ShouldBe(409);
        dup.FirstMessage.ShouldContain("name");

        var moved = await Call(HttpMethod.Put, $"{Base}/{productId}", token, new { categoryId = catB, name = "Antivirus Pro", isActive = true });
        moved.Status.ShouldBe(200);
        moved.Data.GetProperty("categoryId").GetInt64().ShouldBe(catB);

        (await Call(HttpMethod.Put, $"{Base}/{productId}", token, new { categoryId = 999999999, name = "x", isActive = true })).Status.ShouldBe(404);
        (await Call(HttpMethod.Put, $"{Base}/999999999", token, new { categoryId = catB, name = "x", isActive = true })).Status.ShouldBe(404);
    }

    [Fact]
    public async Task Product_CannotBeAddedTo_OrMovedInto_InactiveCategory()
    {
        var token = await AdminTokenAsync();
        var active = await CategoryAsync(token);
        var inactive = await CategoryAsync(token);
        var productId = await ProductAsync(token, active);
        (await Call(HttpMethod.Put, $"{Base}/categories/{inactive}", token, new { name = Uniq("Off "), isActive = false })).Status.ShouldBe(200);

        var add = await Call(HttpMethod.Post, Base, token, new { categoryId = inactive, code = Uniq("P"), name = "x" });
        add.Status.ShouldBe(400);
        add.FirstMessage.ShouldContain("inactive");
        (await Call(HttpMethod.Put, $"{Base}/{productId}", token, new { categoryId = inactive, name = "x", isActive = true })).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, Base, token, new { categoryId = 999999999, code = Uniq("P"), name = "x" })).Status.ShouldBe(404);
    }

    // ---------------- plans ----------------

    [Fact]
    public async Task Plan_CreateUpdate_PriceChangeIsAudited()
    {
        var token = await AdminTokenAsync();
        var productId = await ProductAsync(token, await CategoryAsync(token));

        var created = await Call(HttpMethod.Post, $"{Base}/{productId}/plans", token, Plan("Business Yearly", 24999.50m, 18000m));
        created.Status.ShouldBe(201);
        created.Data.GetProperty("price").GetDecimal().ShouldBe(24999.50m);
        created.Data.GetProperty("cost").GetDecimal().ShouldBe(18000m);
        created.Data.GetProperty("durationUnit").GetString().ShouldBe("Year");
        var planId = created.Data.GetProperty("id").GetInt64();

        (await Call(HttpMethod.Post, $"{Base}/{productId}/plans", token, Plan("Business Yearly"))).Status.ShouldBe(409);

        var updated = await Call(HttpMethod.Put, $"{Base}/plans/{planId}", token,
            new { name = "Business Yearly", durationUnit = "Year", durationCount = 1, price = 26000m, cost = 19000m, sortOrder = 1, isActive = true });
        updated.Status.ShouldBe(200);
        updated.Data.GetProperty("price").GetDecimal().ShouldBe(26000m);

        var audit = await _fx.Db.QueryAsync("SELECT OldValues, NewValues FROM dbo.AuditLogs WHERE EntityType = 'ProductPlan' AND Action = 'Update' AND EntityId = @id",
            r => (Old: r.GetString(0), New: r.GetString(1)), new[] { TestDb.P("@id", planId.ToString()) });
        audit.Count.ShouldBe(1);
        JsonDocument.Parse(audit[0].Old).RootElement.GetProperty("Price").GetDecimal().ShouldBe(24999.50m);
        JsonDocument.Parse(audit[0].New).RootElement.GetProperty("Price").GetDecimal().ShouldBe(26000m);
    }

    [Theory]
    [InlineData("", 100, 50, "Year", 1)]
    [InlineData("Plan", -1, 50, "Year", 1)]
    [InlineData("Plan", 100, -5, "Year", 1)]
    [InlineData("Plan", 100.123, 50, "Year", 1)]
    [InlineData("Plan", 100, 50, "Week", 1)]
    [InlineData("Plan", 100, 50, "Month", 0)]
    [InlineData("Plan", 100, 50, "Month", 121)]
    public async Task Plan_InvalidInput_Returns400(string name, double price, double cost, string unit, int count)
    {
        var token = await AdminTokenAsync();
        var productId = await ProductAsync(token, await CategoryAsync(token));
        var res = await Call(HttpMethod.Post, $"{Base}/{productId}/plans", token, Plan(name, (decimal)price, (decimal)cost, unit, count));
        res.Status.ShouldBe(400);
    }

    [Fact]
    public async Task Plan_MissingPriceOrCost_Returns400_AndUnknownOrInactiveProductIsRefused()
    {
        var token = await AdminTokenAsync();
        var category = await CategoryAsync(token);
        var productId = await ProductAsync(token, category);

        (await Call(HttpMethod.Post, $"{Base}/{productId}/plans", token, new { name = "No price", durationUnit = "Year", durationCount = 1, cost = 1 })).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Base}/{productId}/plans", token, new { name = "No cost", durationUnit = "Year", durationCount = 1, price = 1 })).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Base}/999999999/plans", token, Plan())).Status.ShouldBe(404);

        (await Call(HttpMethod.Put, $"{Base}/{productId}", token, new { categoryId = category, name = Uniq("Off "), isActive = false })).Status.ShouldBe(200);
        (await Call(HttpMethod.Post, $"{Base}/{productId}/plans", token, Plan())).Status.ShouldBe(400);
    }

    [Fact]
    public async Task Catalog_HidesInactiveItems_UnlessIncludeInactive()
    {
        var token = await AdminTokenAsync();
        var category = await CategoryAsync(token);
        var productId = await ProductAsync(token, category);
        var plan = await Call(HttpMethod.Post, $"{Base}/{productId}/plans", token, Plan("Monthly", 1000m, 700m, "Month"));
        var planId = plan.Data.GetProperty("id").GetInt64();
        (await Call(HttpMethod.Post, $"{Base}/{productId}/plans", token, Plan("Yearly", 10000m, 7000m))).Status.ShouldBe(201);

        (await Call(HttpMethod.Put, $"{Base}/plans/{planId}", token,
            new { name = "Monthly", durationUnit = "Month", durationCount = 1, price = 1000m, cost = 700m, isActive = false })).Status.ShouldBe(200);

        var active = (await Call(HttpMethod.Get, $"{Base}/catalog", token)).Data;
        Find(active, category, productId)!.Value.GetProperty("plans").GetArrayLength().ShouldBe(1);
        var all = (await Call(HttpMethod.Get, $"{Base}/catalog?includeInactive=true", token)).Data;
        Find(all, category, productId)!.Value.GetProperty("plans").GetArrayLength().ShouldBe(2);

        (await Call(HttpMethod.Put, $"{Base}/categories/{category}", token, new { name = Uniq("Hidden "), isActive = false })).Status.ShouldBe(200);
        Find((await Call(HttpMethod.Get, $"{Base}/catalog", token)).Data, category).ShouldBeNull();
        Find((await Call(HttpMethod.Get, $"{Base}/catalog?includeInactive=true", token)).Data, category).ShouldNotBeNull();
    }
}
