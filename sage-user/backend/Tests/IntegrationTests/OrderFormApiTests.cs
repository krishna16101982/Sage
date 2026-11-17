using System.Text.Json;
using SageUserApi.Tests.Infrastructure;
using Shouldly;

namespace SageUserApi.Tests.IntegrationTests;

/// <summary>The New order form: lists and defaults (master table + settings), saving an order, reading its details.</summary>
[Collection(UserApiFixture.CollectionName)]
public class OrderFormApiTests
{
    private readonly UserApiFixture _fx;
    public OrderFormApiTests(UserApiFixture fx) { _fx = fx; }

    private const string Api = "/api/user";
    private Task<ApiResult> Call(HttpMethod m, string url, object? body = null, string? bearer = null)
        => UserHttp.SendAsync(_fx.CreateClient(), m, url, body, bearer);
    private async Task<string> TokenAsync(SalesUserRow u) => (await UserHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;
    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    private async Task<long> PlanAsync(decimal price = 1000m, decimal cost = 600m)
    {
        var categoryId = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductCategories (Code, Name) OUTPUT INSERTED.Id VALUES (@c, @n)",
            new[] { TestDb.P("@c", Unique("Cat")), TestDb.P("@n", Unique("Category ")) });
        var productId = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.Products (CategoryId, Code, Name) OUTPUT INSERTED.Id VALUES (@cat, @c, @n)",
            new[] { TestDb.P("@cat", categoryId), TestDb.P("@c", Unique("Prd")), TestDb.P("@n", Unique("Product ")) });
        return await _fx.Db.ExecuteScalarAsync<long>(
            "INSERT dbo.ProductPlans (ProductId, Name, DurationUnit, DurationCount, Price, Cost) OUTPUT INSERTED.Id VALUES (@p, N'Yearly', 'Year', 1, @pr, @co)",
            new[] { TestDb.P("@p", productId), TestDb.P("@pr", price), TestDb.P("@co", cost) });
    }

    private async Task<Guid> CustomerAsync(string token)
    {
        var res = await Call(HttpMethod.Post, $"{Api}/customers", new { name = Unique("Absoft "), city = "Mumbai" }, token);
        res.Status.ShouldBe(201, res.Root.ToString());
        return res.Data.GetProperty("id").GetGuid();
    }

    private static int DefaultId(JsonElement options, string list)
        => options.GetProperty(list).EnumerateArray().Single(o => o.GetProperty("isDefault").GetBoolean()).GetProperty("id").GetInt32();

    private static int IdOf(JsonElement options, string list, string name)
        => options.GetProperty(list).EnumerateArray().Single(o => o.GetProperty("name").GetString() == name).GetProperty("id").GetInt32();

    private static Dictionary<string, object?> Order(Guid customerId, long planId, JsonElement options, Action<Dictionary<string, object?>>? change = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["customerId"] = customerId, ["planId"] = planId, ["quantity"] = 11, ["discountAmount"] = 1100m,
            ["startDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["gstPercent"] = 18m, ["freeMonths"] = 2,
            ["billingFrequencyId"] = DefaultId(options, "billingFrequencies"), ["billingMethodId"] = DefaultId(options, "billingMethods"),
            ["paymentMethodId"] = DefaultId(options, "paymentMethods"), ["paymentTermId"] = IdOf(options, "paymentTerms", "Net 30"),
            ["currencyId"] = DefaultId(options, "currencies"),
            ["primaryContact"] = new { name = "Mr. Abhishek Awasthi", designation = "Founder & CEO", email = "abhi@absoft.example.com", phone = "9984818226" },
            ["billingSameAsPrimary"] = true,
            ["billToAddress"] = "Floor 2, WeWork, Vaswani Chambers, Annie Besant Road, Mumbai 400030",
            ["wording"] = new
            {
                signReturnLine = "Sign and return to i2k2 Networks Pvt Ltd",
                orderNotes = "Custom note <b>for this order</b> only.",
                freeMonthNote = "This order includes 2 free months at no cost as a one-time offering.",
                productTitle = "Sales Navigator Advanced",
                taxInformation = "Tax Information: 18% GST will be applicable.",
                signatureClauses = "Clause one.\nClause two.",
                footer = "Thank you.\ni2k2 Networks Proprietary and Confidential"
            }
        };
        change?.Invoke(body);
        return body;
    }

    [Fact]
    public async Task Options_ListActiveMasterValues_WithOneDefaultEach_AndSettings()
    {
        var token = await TokenAsync(await SalesData.CreateAsync(_fx.Db));
        var res = await Call(HttpMethod.Get, $"{Api}/order-form/options", bearer: token);
        res.Status.ShouldBe(200, res.Root.ToString());
        res.Data.GetProperty("defaultGstPercent").GetDecimal().ShouldBe(18m);
        res.Data.GetProperty("maxFreeMonths").GetInt32().ShouldBe(12);
        var wording = res.Data.GetProperty("wording");
        wording.GetProperty("signReturnLine").GetString().ShouldBe("Sign and return to i2k2 Networks Pvt Ltd");
        wording.GetProperty("freeMonthNote").GetString()!.ShouldContain("{freeMonths}");
        wording.GetProperty("taxInformation").GetString()!.ShouldContain("{gstPercent}%");
        wording.GetProperty("signatureClauses").GetString()!.Split('\n').Length.ShouldBe(5);
        foreach (var list in new[] { "billingFrequencies", "billingMethods", "paymentMethods", "paymentTerms", "currencies" })
            res.Data.GetProperty(list).EnumerateArray().Count(o => o.GetProperty("isDefault").GetBoolean()).ShouldBe(1, list);
        res.Data.GetProperty("billingFrequencies").EnumerateArray().Select(o => o.GetProperty("name").GetString())
            .ShouldBe(new[] { "Monthly", "Quarterly", "Half-yearly", "Annual", "One-time" });
        (await Call(HttpMethod.Get, $"{Api}/order-form/options")).Status.ShouldBe(401);
    }

    [Fact]
    public async Task CreateOrder_SavesTax_FreeMonths_Terms_AndContacts_InTheSalesTable()
    {
        var manager = await SalesData.CreateAsync(_fx.Db);
        var person = await SalesData.CreateAsync(_fx.Db, "SalesPerson", manager);
        var token = await TokenAsync(person);
        var options = (await Call(HttpMethod.Get, $"{Api}/order-form/options", bearer: token)).Data;
        var plan = await PlanAsync(price: 1000m, cost: 600m);
        var customer = await CustomerAsync(token);

        var res = await Call(HttpMethod.Post, $"{Api}/order-form", Order(customer, plan, options), token);
        res.Status.ShouldBe(201, res.Root.ToString());
        res.FirstMessage.ShouldBe("Order saved.");
        var sale = res.Data;
        // Same price-list rules as a simple sale: 11 x 1000 = 11000, discount 1100 -> 9900.
        sale.GetProperty("listAmount").GetDecimal().ShouldBe(11000m);
        sale.GetProperty("saleAmount").GetDecimal().ShouldBe(9900m);
        // 1 year + 2 free months.
        var start = DateOnly.FromDateTime(DateTime.UtcNow);
        sale.GetProperty("renewalDate").GetString().ShouldBe(start.AddYears(1).AddMonths(2).ToString("yyyy-MM-dd"));

        var id = sale.GetProperty("id").GetGuid();
        var details = await Call(HttpMethod.Get, $"{Api}/order-form/{id}", bearer: token);
        details.Status.ShouldBe(200, details.Root.ToString());
        var d = details.Data;
        d.GetProperty("hasOrderForm").GetBoolean().ShouldBeTrue();
        d.GetProperty("gstPercent").GetDecimal().ShouldBe(18m);
        d.GetProperty("taxAmount").GetDecimal().ShouldBe(1782m);           // 18% of 9900
        d.GetProperty("totalAmount").GetDecimal().ShouldBe(11682m);
        d.GetProperty("freeMonths").GetInt32().ShouldBe(2);
        d.GetProperty("billingFrequency").GetString().ShouldBe("Annual");
        d.GetProperty("paymentTerm").GetString().ShouldBe("Net 30");
        d.GetProperty("currency").GetString().ShouldBe("INR");
        d.GetProperty("primaryContact").GetProperty("designation").GetString().ShouldBe("Founder & CEO");
        d.GetProperty("billingContact").GetProperty("email").GetString().ShouldBe("abhi@absoft.example.com");   // same as primary
        d.GetProperty("billToAddress").GetString()!.ShouldStartWith("Floor 2, WeWork");
        // The wording is kept as sent for this order (tags removed, lines kept).
        var w = d.GetProperty("wording");
        w.GetProperty("orderNotes").GetString().ShouldBe("Custom note for this order only.");
        w.GetProperty("freeMonthNote").GetString()!.ShouldContain("2 free months");
        w.GetProperty("signatureClauses").GetString().ShouldBe("Clause one.\nClause two.");
        w.GetProperty("extraItemTitle").ValueKind.ShouldBe(JsonValueKind.Null);

        // The manager sees it too; another seller does not.
        (await Call(HttpMethod.Get, $"{Api}/order-form/{id}", bearer: await TokenAsync(manager))).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, $"{Api}/order-form/{id}", bearer: await TokenAsync(await SalesData.CreateAsync(_fx.Db)))).Status.ShouldBe(404);
        // The sale is an ordinary sale everywhere else.
        (await Call(HttpMethod.Get, $"{Api}/sales/{id}", bearer: token)).Data.GetProperty("saleNumber").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SeatPricedPlan_CatalogHasThisYearsRanges_AndTheOrderUsesTheRangeForTheSeats()
    {
        var token = await TokenAsync(await SalesData.CreateAsync(_fx.Db));
        var options = (await Call(HttpMethod.Get, $"{Api}/order-form/options", bearer: token)).Data;
        var plan = await PlanAsync(price: 1000m, cost: 600m);
        await _fx.Db.ExecuteNonQueryAsync(
            "UPDATE dbo.ProductPlans SET IncludedItemTitle = N'Sales Navigator Team Link Extend Seat', IncludedItemDescription = N'{totalMonths} months 1000 Team Link Extend seats' WHERE Id = @p",
            new[] { TestDb.P("@p", plan) });
        var india = DateTime.UtcNow.AddMinutes(330);
        int fy = india.Month >= 4 ? india.Year + 1 : india.Year;
        // This year: 1-9 at 111578, 10-19 at 107116 (cost 90000), 20+ at 100000. Last year is ignored.
        await _fx.Db.ExecuteNonQueryAsync(
            "INSERT dbo.ProductPlanSeatPrices (PlanId, FiscalYear, MinSeats, MaxSeats, Price, Cost) VALUES " +
            "(@p, @fy, 1, 9, 111578, NULL), (@p, @fy, 10, 19, 107116, 90000), (@p, @fy, 20, NULL, 100000, NULL), (@p, @fy - 1, 1, NULL, 5, NULL)",
            new[] { TestDb.P("@p", plan), TestDb.P("@fy", fy) });

        var catalogPlan = (await Call(HttpMethod.Get, $"{Api}/catalog", bearer: token)).Data.EnumerateArray()
            .SelectMany(c => c.GetProperty("products").EnumerateArray()).SelectMany(p => p.GetProperty("plans").EnumerateArray())
            .Single(p => p.GetProperty("id").GetInt64() == plan);
        catalogPlan.GetProperty("hasSeatPrices").GetBoolean().ShouldBeTrue();
        catalogPlan.GetProperty("includedItemTitle").GetString().ShouldBe("Sales Navigator Team Link Extend Seat");
        catalogPlan.GetProperty("includedItemDescription").GetString().ShouldBe("{totalMonths} months 1000 Team Link Extend seats");
        catalogPlan.GetProperty("priceFiscalYear").GetInt32().ShouldBe(fy);
        var ranges = catalogPlan.GetProperty("seatPrices");
        ranges.GetArrayLength().ShouldBe(3);
        ranges[0].GetProperty("cost").GetDecimal().ShouldBe(600m);          // blank cost = the plan's cost
        ranges[1].GetProperty("cost").GetDecimal().ShouldBe(90000m);
        ranges[2].GetProperty("maxSeats").ValueKind.ShouldBe(JsonValueKind.Null);

        // 11 seats -> the 10-19 range: 11 x 107116 = 1178276, less the 1100 discount.
        var res = await Call(HttpMethod.Post, $"{Api}/order-form", Order(await CustomerAsync(token), plan, options), token);
        res.Status.ShouldBe(201, res.Root.ToString());
        res.Data.GetProperty("listAmount").GetDecimal().ShouldBe(1178276m);
        res.Data.GetProperty("saleAmount").GetDecimal().ShouldBe(1177176m);
        var d = (await Call(HttpMethod.Get, $"{Api}/order-form/{res.Data.GetProperty("id").GetGuid()}", bearer: token)).Data;
        d.GetProperty("priceFiscalYear").GetInt32().ShouldBe(fy);
        d.GetProperty("priceSeatRange").GetString().ShouldBe("10-19");
    }

    [Fact]
    public async Task CreateOrder_RefusesWrongValues_AndSavesNothing()
    {
        var person = await SalesData.CreateAsync(_fx.Db);
        var token = await TokenAsync(person);
        var options = (await Call(HttpMethod.Get, $"{Api}/order-form/options", bearer: token)).Data;
        var plan = await PlanAsync();
        var customer = await CustomerAsync(token);
        int Count() => _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Sales WHERE SalesUserId = @u", new[] { TestDb.P("@u", person.Id) }).Result;

        (await Call(HttpMethod.Post, $"{Api}/order-form", Order(customer, plan, options, b => b["gstPercent"] = 30m), token)).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Api}/order-form", Order(customer, plan, options, b => b["primaryContact"] = new { name = "" }), token)).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Api}/order-form", Order(customer, plan, options, b => b["primaryContact"] = new { name = "A", email = "not-an-email" }), token)).Status.ShouldBe(400);
        // A list value of the wrong list, and too many free months: the procedure refuses before anything is saved.
        var wrongList = await Call(HttpMethod.Post, $"{Api}/order-form", Order(customer, plan, options, b => b["paymentTermId"] = DefaultId(options, "billingMethods")), token);
        wrongList.Status.ShouldBe(400);
        wrongList.FirstMessage.ShouldBe("Choose a payment term from the list");
        var tooMany = await Call(HttpMethod.Post, $"{Api}/order-form", Order(customer, plan, options, b => b["freeMonths"] = 13), token);
        tooMany.Status.ShouldBe(400);
        tooMany.FirstMessage.ShouldContain("between 0 and 12");
        // Price-list rules from usp_Sale_Create still apply (discount above the list amount).
        (await Call(HttpMethod.Post, $"{Api}/order-form", Order(customer, plan, options, b => b["discountAmount"] = 999999m), token)).Status.ShouldBe(400);
        Count().ShouldBe(0);
    }

    [Fact]
    public async Task FreeMonthNote_IsDropped_WithoutFreeMonths()
    {
        var token = await TokenAsync(await SalesData.CreateAsync(_fx.Db));
        var options = (await Call(HttpMethod.Get, $"{Api}/order-form/options", bearer: token)).Data;
        var res = await Call(HttpMethod.Post, $"{Api}/order-form", Order(await CustomerAsync(token), await PlanAsync(), options, b => b["freeMonths"] = 0), token);
        res.Status.ShouldBe(201, res.Root.ToString());
        var w = (await Call(HttpMethod.Get, $"{Api}/order-form/{res.Data.GetProperty("id").GetGuid()}", bearer: token)).Data.GetProperty("wording");
        w.GetProperty("freeMonthNote").ValueKind.ShouldBe(JsonValueKind.Null);
        w.GetProperty("signReturnLine").GetString().ShouldBe("Sign and return to i2k2 Networks Pvt Ltd");
    }

    [Fact]
    public async Task SimpleSale_IsUnchanged_AndHasNoOrderForm()
    {
        var token = await TokenAsync(await SalesData.CreateAsync(_fx.Db));
        var plan = await PlanAsync();
        var customer = await CustomerAsync(token);
        var sale = await Call(HttpMethod.Post, $"{Api}/sales",
            new { customerId = customer, planId = plan, quantity = 1, saleAmount = 1000m, startDate = DateTime.UtcNow.ToString("yyyy-MM-dd") }, token);
        sale.Status.ShouldBe(201, sale.Root.ToString());
        sale.Data.GetProperty("renewalDate").GetString().ShouldBe(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1).ToString("yyyy-MM-dd"));

        var details = await Call(HttpMethod.Get, $"{Api}/order-form/{sale.Data.GetProperty("id").GetGuid()}", bearer: token);
        details.Status.ShouldBe(200);
        details.Data.GetProperty("hasOrderForm").GetBoolean().ShouldBeFalse();
        details.Data.GetProperty("taxAmount").GetDecimal().ShouldBe(0m);
        details.Data.GetProperty("totalAmount").GetDecimal().ShouldBe(1000m);
        details.Data.GetProperty("wording").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
