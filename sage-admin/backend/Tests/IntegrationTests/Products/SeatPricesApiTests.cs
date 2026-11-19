using System.Text.Json;
using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Products;

/// <summary>
/// Seat prices per financial year (DDL 032): Admin Panel entry, and the order form's procedure charging every seat at the
/// price of the range the seat count falls in, for the financial year of the order date.
/// </summary>
[Collection(ApiFixture.CollectionName)]
public class SeatPricesApiTests
{
    private readonly ApiFixture _fx;
    public SeatPricesApiTests(ApiFixture fx) { _fx = fx; }

    private const string Base = "/api/admin/products";
    private static string Uniq(string prefix) => (prefix + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();

    /// <summary>The financial year of today in India: Apr 2026 - Mar 2027 = 2027.</summary>
    private static int CurrentFy()
    {
        var india = DateTime.UtcNow.AddMinutes(330);
        return india.Month >= 4 ? india.Year + 1 : india.Year;
    }

    private async Task<string> TokenAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;
    }

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    /// <summary>A new active plan (price 1000, cost 400) in a new category and product.</summary>
    private async Task<long> PlanAsync(string name = "Advanced")
    {
        var cat = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductCategories (Code, Name) OUTPUT INSERTED.Id VALUES (@c, @c)", new[] { TestDb.P("@c", Uniq("C")) });
        var prod = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.Products (CategoryId, Code, Name) OUTPUT INSERTED.Id VALUES (@cat, @c, @c)", new[] { TestDb.P("@cat", cat), TestDb.P("@c", Uniq("P")) });
        return await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductPlans (ProductId, Name, Price, Cost) OUTPUT INSERTED.Id VALUES (@p, @n, 1000, 400)",
            new[] { TestDb.P("@p", prod), TestDb.P("@n", name) });
    }

    /// <summary>A small LinkedIn-style table: 1-9, 10-19, 20-99, 100-199, 200+.</summary>
    private static object[] Ranges(decimal factor = 1m) => new object[]
    {
        new { minSeats = 1, maxSeats = (int?)9, price = 111578m * factor, cost = (decimal?)90000m },
        new { minSeats = 10, maxSeats = (int?)19, price = 107116m * factor, cost = (decimal?)null },
        new { minSeats = 20, maxSeats = (int?)99, price = 100000m * factor, cost = (decimal?)null },
        new { minSeats = 100, maxSeats = (int?)199, price = 95000m * factor, cost = (decimal?)null },
        new { minSeats = 200, maxSeats = (int?)null, price = 90000m * factor, cost = (decimal?)null },
    };

    [Theory]
    [InlineData("GET", Base + "/plans/1/seat-prices")]
    [InlineData("PUT", Base + "/plans/1/seat-prices/2027")]
    public async Task WithoutToken_Returns401(string method, string url)
        => (await Call(new HttpMethod(method), url, null, method == "GET" ? null : new { })).Status.ShouldBe(401);

    [Fact]
    public async Task ViewCanRead_EditCanSave_UnknownPlanIs404()
    {
        var plan = await PlanAsync();
        var viewer = await TokenAsync("Product.View");
        (await Call(HttpMethod.Get, $"{Base}/plans/{plan}/seat-prices", viewer)).Status.ShouldBe(200);
        (await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/2027", viewer, new { ranges = Ranges() })).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, $"{Base}/plans/{plan}/seat-prices", await TokenAsync("Customer.View"))).Status.ShouldBe(403);

        var editor = await TokenAsync("Product.View", "Product.Edit");
        (await Call(HttpMethod.Get, $"{Base}/plans/999999999/seat-prices", editor)).Status.ShouldBe(404);
        (await Call(HttpMethod.Put, $"{Base}/plans/999999999/seat-prices/2027", editor, new { ranges = Ranges() })).Status.ShouldBe(404);
    }

    [Fact]
    public async Task Year_IsSaved_Read_Replaced_Removed_AndAudited()
    {
        var plan = await PlanAsync();
        var token = await TokenAsync("Product.View", "Product.Edit");

        var saved = await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/2026", token, new { ranges = Ranges() });
        saved.Status.ShouldBe(200, saved.Root.ToString());
        saved.Data.GetProperty("fiscalYear").GetInt32().ShouldBe(2026);
        saved.Data.GetProperty("ranges").GetArrayLength().ShouldBe(5);
        saved.Data.GetProperty("ranges")[4].GetProperty("maxSeats").ValueKind.ShouldBe(JsonValueKind.Null);
        saved.Data.GetProperty("ranges")[0].GetProperty("cost").GetDecimal().ShouldBe(90000m);

        // Next year at +5%; both years are listed, newest first.
        (await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/2027", token, new { ranges = Ranges(1.05m) })).Status.ShouldBe(200);
        var years = await Call(HttpMethod.Get, $"{Base}/plans/{plan}/seat-prices", token);
        years.Data.EnumerateArray().Select(y => y.GetProperty("fiscalYear").GetInt32()).ShouldBe(new[] { 2027, 2026 });
        years.Data[0].GetProperty("ranges")[0].GetProperty("price").GetDecimal().ShouldBe(117156.90m);

        // Replacing a year replaces all its ranges.
        var two = new object[] { new { minSeats = 1, maxSeats = (int?)49, price = 500m }, new { minSeats = 50, maxSeats = (int?)null, price = 450m } };
        (await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/2026", token, new { ranges = two })).Data.GetProperty("ranges").GetArrayLength().ShouldBe(2);

        // The catalog marks the plan; removing every year clears the mark.
        bool HasSeatPrices(JsonElement catalog) => catalog.EnumerateArray().SelectMany(c => c.GetProperty("products").EnumerateArray())
            .SelectMany(p => p.GetProperty("plans").EnumerateArray()).Single(p => p.GetProperty("id").GetInt64() == plan).GetProperty("hasSeatPrices").GetBoolean();
        HasSeatPrices((await Call(HttpMethod.Get, $"{Base}/catalog?includeInactive=true", token)).Data).ShouldBeTrue();
        (await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/2026", token, new { ranges = Array.Empty<object>() })).Status.ShouldBe(200);
        (await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/2027", token, new { ranges = Array.Empty<object>() })).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, $"{Base}/plans/{plan}/seat-prices", token)).Data.GetArrayLength().ShouldBe(0);
        HasSeatPrices((await Call(HttpMethod.Get, $"{Base}/catalog?includeInactive=true", token)).Data).ShouldBeFalse();

        var audit = await _fx.Db.QueryAsync("SELECT NewValues FROM dbo.AuditLogs WHERE EntityType = 'ProductPlan' AND Action = 'Update' AND EntityId = @id",
            r => r.GetString(0), new[] { TestDb.P("@id", plan.ToString()) });
        audit.Count.ShouldBe(5);
        audit.ShouldAllBe(a => a.Contains("SeatPrices"));
    }

    public static IEnumerable<object[]> BadRanges() => new[]
    {
        new object[] { "starts at 2", new object[] { new { minSeats = 2, maxSeats = (int?)null, price = 10m } } },
        new object[] { "gap", new object[] { new { minSeats = 1, maxSeats = (int?)9, price = 10m }, new { minSeats = 11, maxSeats = (int?)null, price = 9m } } },
        new object[] { "overlap", new object[] { new { minSeats = 1, maxSeats = (int?)9, price = 10m }, new { minSeats = 9, maxSeats = (int?)null, price = 9m } } },
        new object[] { "open-ended in the middle", new object[] { new { minSeats = 1, maxSeats = (int?)null, price = 10m }, new { minSeats = 10, maxSeats = (int?)20, price = 9m } } },
        new object[] { "ends before it starts", new object[] { new { minSeats = 5, maxSeats = (int?)4, price = 10m } } },
        new object[] { "negative price", new object[] { new { minSeats = 1, maxSeats = (int?)null, price = -1m } } },
        new object[] { "3 decimals", new object[] { new { minSeats = 1, maxSeats = (int?)null, price = 1.005m } } },
        new object[] { "no price", new object[] { new { minSeats = 1, maxSeats = (int?)null } } },
    };

    [Theory]
    [MemberData(nameof(BadRanges))]
    public async Task InvalidRanges_Return400_AndKeepTheOldPrices(string why, object[] ranges)
    {
        var plan = await PlanAsync();
        var token = await TokenAsync("Product.View", "Product.Edit");
        (await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/2027", token, new { ranges = Ranges() })).Status.ShouldBe(200);

        var res = await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/2027", token, new { ranges });
        res.Status.ShouldBe(400, why + ": " + res.Root);
        (await Call(HttpMethod.Get, $"{Base}/plans/{plan}/seat-prices", token)).Data[0].GetProperty("ranges").GetArrayLength().ShouldBe(5);
    }

    [Fact]
    public async Task InvalidYear_Returns400()
    {
        var plan = await PlanAsync();
        var token = await TokenAsync("Product.View", "Product.Edit");
        (await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/1999", token, new { ranges = Ranges() })).Status.ShouldBe(400);
    }

    // ---------------- The order form's procedure ----------------

    private async Task<(long seller, Guid customer)> SellerAndCustomerAsync()
    {
        var u = ("s" + Guid.NewGuid().ToString("N"))[..16];
        var seller = await _fx.Db.ExecuteScalarAsync<long>(
            "INSERT dbo.SalesUsers (FirstName, LastName, Username, Email, PasswordHash, MustChangePassword, Role) OUTPUT INSERTED.Id VALUES (N'Sam', @u, @u, @e, N'x', 0, 'SalesManager')",
            new[] { TestDb.P("@u", u), TestDb.P("@e", u + "@sales.example.com") });
        var customer = Guid.NewGuid();
        await _fx.Db.ExecuteNonQueryAsync("INSERT dbo.Customers (PublicId, Name, City) VALUES (@id, @n, N'Pune')", new[] { TestDb.P("@id", customer), TestDb.P("@n", "Cust " + Uniq("X")) });
        return (seller, customer);
    }

    private async Task<Guid> OrderAsync(long seller, Guid customer, long plan, int seats, decimal? discount = 0m, decimal? saleAmount = null, int? paidYears = null)
    {
        async Task<int> Default(string type) => await _fx.Db.ExecuteScalarAsync<int>("SELECT Id FROM dbo.MasterValues WHERE MasterType = @t AND IsDefault = 1", new[] { TestDb.P("@t", type) });
        return await _fx.Db.ExecuteProcedureScalarAsync<Guid>("dbo.usp_SaleOrder_Create", new[]
        {
            TestDb.P("@SalesUserId", seller), TestDb.P("@CustomerPublicId", customer), TestDb.P("@PlanId", plan), TestDb.P("@Quantity", seats),
            TestDb.P("@SaleAmount", (object?)saleAmount ?? DBNull.Value), TestDb.P("@DiscountAmount", (object?)discount ?? DBNull.Value),
            TestDb.P("@DurationUnit", paidYears.HasValue ? "Year" : DBNull.Value), TestDb.P("@DurationCount", (object?)paidYears ?? DBNull.Value),
            TestDb.P("@StartDate", DateTime.UtcNow.Date), TestDb.P("@GstPercent", 18m),
            TestDb.P("@BillingFrequencyId", await Default("BillingFrequency")), TestDb.P("@BillingMethodId", await Default("BillingMethod")),
            TestDb.P("@PaymentMethodId", await Default("PaymentMethod")), TestDb.P("@PaymentTermId", await Default("PaymentTerm")),
            TestDb.P("@CurrencyId", await Default("Currency")), TestDb.P("@PrimaryContactName", "Asha Rao")
        });
    }

    private async Task<(decimal unitPrice, decimal unitCost, decimal list, decimal sale, decimal discount, decimal tax, int? fy, string? range, decimal costAmount)> SaleAsync(Guid id)
        => (await _fx.Db.QueryAsync(
            "SELECT UnitPrice, UnitCost, ListAmount, SaleAmount, DiscountAmount, TaxAmount, PriceFiscalYear, PriceSeatRange, CostAmount FROM dbo.Sales WHERE PublicId = @id",
            r => (r.GetDecimal(0), r.GetDecimal(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5),
                  r.IsDBNull(6) ? (int?)null : r.GetInt32(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetDecimal(8)),
            new[] { TestDb.P("@id", id) })).Single();

    /// <summary>Every seat is charged the per-seat price of the range the seat count falls in.</summary>
    [Theory]
    [InlineData(8, "1-9", 111578, 90000)]       // the range's own cost per seat
    [InlineData(12, "10-19", 107116, 400)]      // no range cost: the plan's cost per seat
    [InlineData(150, "100-199", 95000, 400)]
    [InlineData(5000, "200+", 90000, 400)]
    public async Task Order_ChargesEverySeat_AtTheRangePrice_ForThisFinancialYear(int seats, string range, int price, int cost)
    {
        var plan = await PlanAsync();
        var token = await TokenAsync("Product.View", "Product.Edit");
        (await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/{CurrentFy()}", token, new { ranges = Ranges() })).Status.ShouldBe(200);
        // Another year's prices must not be used.
        (await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/{CurrentFy() - 1}", token, new { ranges = Ranges(0.5m) })).Status.ShouldBe(200);
        var (seller, customer) = await SellerAndCustomerAsync();

        var sale = await SaleAsync(await OrderAsync(seller, customer, plan, seats, discount: 1000m));
        sale.unitPrice.ShouldBe(price);
        sale.unitCost.ShouldBe(cost);
        sale.list.ShouldBe(price * (decimal)seats);
        sale.costAmount.ShouldBe(cost * (decimal)seats);
        sale.discount.ShouldBe(1000m);
        sale.sale.ShouldBe(price * (decimal)seats - 1000m);
        sale.tax.ShouldBe(Math.Round(sale.sale * 0.18m, 2));
        sale.fy.ShouldBe(CurrentFy());
        sale.range.ShouldBe(range);
    }

    [Fact]
    public async Task Order_RefusesAPlanWithoutPricesForThisYear_AndAmountsAboveTheList()
    {
        var token = await TokenAsync("Product.View", "Product.Edit");
        var (seller, customer) = await SellerAndCustomerAsync();

        var lastYearOnly = await PlanAsync();
        (await Call(HttpMethod.Put, $"{Base}/plans/{lastYearOnly}/seat-prices/{CurrentFy() - 1}", token, new { ranges = Ranges() })).Status.ShouldBe(200);
        var ex = await Should.ThrowAsync<SageCommonLib.Models.AppException>(() => OrderAsync(seller, customer, lastYearOnly, 5));
        ex.Message.ShouldContain($"FY{CurrentFy() % 100:00}");

        // Bounded last range: more seats than any range covers.
        var bounded = await PlanAsync();
        (await Call(HttpMethod.Put, $"{Base}/plans/{bounded}/seat-prices/{CurrentFy()}", token, new { ranges = new object[] { new { minSeats = 1, maxSeats = (int?)9, price = 100m } } })).Status.ShouldBe(200);
        (await Should.ThrowAsync<SageCommonLib.Models.AppException>(() => OrderAsync(seller, customer, bounded, 10))).Message.ShouldContain("No seat price covers");

        // Sale price above the seat-price list amount (9 x 100 = 900).
        (await Should.ThrowAsync<SageCommonLib.Models.AppException>(() => OrderAsync(seller, customer, bounded, 9, discount: null, saleAmount: 901m))).Message.ShouldContain("more than the list amount");
        var ok = await SaleAsync(await OrderAsync(seller, customer, bounded, 9, discount: null, saleAmount: 900m));
        ok.list.ShouldBe(900m);
        ok.discount.ShouldBe(0m);
    }

    /// <summary>The price is for one plan term (1 year): 24 paid months cost twice, 36 three times, seat-priced or not.</summary>
    [Theory]
    [InlineData(true, 2, 12, 107116)]     // seat range 10-19
    [InlineData(true, 3, 5, 111578)]      // seat range 1-9
    [InlineData(false, 2, 3, 1000)]       // plan price
    [InlineData(false, 3, 3, 1000)]
    public async Task Order_ForSeveralYears_MultipliesThePrice(bool seatPriced, int years, int seats, int pricePerYear)
    {
        var plan = await PlanAsync();
        if (seatPriced)
        {
            var token = await TokenAsync("Product.View", "Product.Edit");
            (await Call(HttpMethod.Put, $"{Base}/plans/{plan}/seat-prices/{CurrentFy()}", token, new { ranges = Ranges() })).Status.ShouldBe(200);
        }
        var (seller, customer) = await SellerAndCustomerAsync();
        var sale = await SaleAsync(await OrderAsync(seller, customer, plan, seats, discount: 100m, paidYears: years));
        sale.unitPrice.ShouldBe(pricePerYear * (decimal)years);
        sale.list.ShouldBe(pricePerYear * (decimal)years * seats);
        sale.sale.ShouldBe(pricePerYear * (decimal)years * seats - 100m);
        sale.range.ShouldBe(seatPriced ? (seats >= 10 ? "10-19" : "1-9") : null);
        var renewal = await _fx.Db.ExecuteScalarAsync<DateTime>("SELECT RenewalDate FROM dbo.Sales WHERE PublicId = (SELECT TOP 1 PublicId FROM dbo.Sales ORDER BY Id DESC)");
        renewal.Date.ShouldBe(DateTime.UtcNow.Date.AddYears(years));
    }

    [Fact]
    public async Task Order_OnAPlanWithoutSeatPrices_KeepsThePlanPrice()
    {
        var plan = await PlanAsync("Yearly");
        var (seller, customer) = await SellerAndCustomerAsync();
        var sale = await SaleAsync(await OrderAsync(seller, customer, plan, 3, discount: 100m));
        sale.unitPrice.ShouldBe(1000m);
        sale.list.ShouldBe(3000m);
        sale.sale.ShouldBe(2900m);
        sale.fy.ShouldBeNull();
        sale.range.ShouldBeNull();
    }
}
