using System.Text.Json;
using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Settings;

/// <summary>Order form lists (dbo.MasterValues) and the order details shown on the Admin sale page.</summary>
[Collection(ApiFixture.CollectionName)]
public class MastersApiTests
{
    private readonly ApiFixture _fx;
    public MastersApiTests(ApiFixture fx) { _fx = fx; }

    private async Task<string> TokenAsync(params string[] permissions)
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        if (permissions.Length > 0) await TestData.AssignGroupAsync(_fx.Db, u, permissions);
        return (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;
    }

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    private static JsonElement List(ApiResult lists, string type)
        => lists.Data.EnumerateArray().Single(l => l.GetProperty("masterType").GetString() == type);

    private static string Code() => ("T" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();

    [Fact]
    public async Task Lists_NeedSettingView_ChangesNeedSettingEdit()
    {
        (await Call(HttpMethod.Get, "/api/admin/masters", await TokenAsync("Customer.View"))).Status.ShouldBe(403);
        var viewer = await TokenAsync("Setting.View");
        var lists = await Call(HttpMethod.Get, "/api/admin/masters", viewer);
        lists.Status.ShouldBe(200);
        lists.Data.EnumerateArray().Select(l => l.GetProperty("masterType").GetString())
            .ShouldBe(new[] { "BillingFrequency", "BillingMethod", "PaymentMethod", "PaymentTerm", "Currency" });
        List(lists, "Currency").GetProperty("values")[0].GetProperty("name").GetString().ShouldBe("INR");
        (await Call(HttpMethod.Post, "/api/admin/masters", viewer, new { masterType = "PaymentTerm", code = Code(), name = "Net 90" })).Status.ShouldBe(403);
    }

    [Fact]
    public async Task Values_AreAdded_Edited_AndTheDefaultMovesSafely()
    {
        var token = await TokenAsync("Setting.View", "Setting.Edit", "AuditLog.View");
        string code = Code(), name = "Net " + code[^4..];

        (await Call(HttpMethod.Post, "/api/admin/masters", token, new { masterType = "Nope", code, name })).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, "/api/admin/masters", token, new { masterType = "PaymentTerm", code = "bad code!", name })).Status.ShouldBe(400);

        var created = await Call(HttpMethod.Post, "/api/admin/masters", token, new { masterType = "PaymentTerm", code, name, sortOrder = 90 });
        created.Status.ShouldBe(201, created.Root.ToString());
        int id = created.Data.GetProperty("id").GetInt32();
        (await Call(HttpMethod.Post, "/api/admin/masters", token, new { masterType = "PaymentTerm", code, name = "Other" })).Status.ShouldBe(409);

        // Making it the default clears the old default; the list keeps exactly one.
        var oldDefault = List(await Call(HttpMethod.Get, "/api/admin/masters", token), "PaymentTerm").GetProperty("values").EnumerateArray()
            .Single(v => v.GetProperty("isDefault").GetBoolean()).GetProperty("id").GetInt32();
        var madeDefault = await Call(HttpMethod.Put, $"/api/admin/masters/{id}", token, new { masterType = "PaymentTerm", code, name, sortOrder = 90, isDefault = true, isActive = true });
        madeDefault.Status.ShouldBe(200, madeDefault.Root.ToString());
        var values = List(await Call(HttpMethod.Get, "/api/admin/masters", token), "PaymentTerm").GetProperty("values").EnumerateArray().ToList();
        values.Count(v => v.GetProperty("isDefault").GetBoolean()).ShouldBe(1);
        values.Single(v => v.GetProperty("isDefault").GetBoolean()).GetProperty("id").GetInt32().ShouldBe(id);

        // The default cannot be turned off; move the default back first, then it can.
        var refused = await Call(HttpMethod.Put, $"/api/admin/masters/{id}", token, new { masterType = "PaymentTerm", code, name, isDefault = true, isActive = false });
        refused.Status.ShouldBe(400);
        var back = values.Single(v => v.GetProperty("id").GetInt32() == oldDefault);
        (await Call(HttpMethod.Put, $"/api/admin/masters/{oldDefault}", token, new
        {
            masterType = "PaymentTerm", code = back.GetProperty("code").GetString(), name = back.GetProperty("name").GetString(),
            sortOrder = back.GetProperty("sortOrder").GetInt32(), isDefault = true, isActive = true
        })).Status.ShouldBe(200);
        (await Call(HttpMethod.Put, $"/api/admin/masters/{id}", token, new { masterType = "PaymentTerm", code, name, isActive = false })).Status.ShouldBe(200);

        var log = await Call(HttpMethod.Get, "/api/admin/audit-logs?entityType=MasterValue", token);
        log.Data.GetProperty("items").EnumerateArray().ShouldContain(e => e.GetProperty("entityId").GetString() == id.ToString());
    }

    [Fact]
    public async Task AdminSalePage_ShowsTheOrderDetails()
    {
        var token = await TokenAsync("Sales.View");
        // An order recorded through the User Panel's procedure (seller, plan, customer inserted directly).
        var u = ("s" + Guid.NewGuid().ToString("N"))[..16];
        var seller = await _fx.Db.ExecuteScalarAsync<long>(
            "INSERT dbo.SalesUsers (FirstName, LastName, Username, Email, PasswordHash, MustChangePassword, Role) OUTPUT INSERTED.Id VALUES (N'Sam', @u, @u, @e, N'x', 0, 'SalesManager')",
            new[] { TestDb.P("@u", u), TestDb.P("@e", u + "@sales.example.com") });
        var cat = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductCategories (Code, Name) OUTPUT INSERTED.Id VALUES (@c, @c)", new[] { TestDb.P("@c", Code()) });
        var prod = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.Products (CategoryId, Code, Name) OUTPUT INSERTED.Id VALUES (@cat, @c, @c)", new[] { TestDb.P("@cat", cat), TestDb.P("@c", Code()) });
        var plan = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductPlans (ProductId, Name, Price, Cost) OUTPUT INSERTED.Id VALUES (@p, N'Yearly', 1000, 400)", new[] { TestDb.P("@p", prod) });
        var customer = Guid.NewGuid();
        await _fx.Db.ExecuteNonQueryAsync("INSERT dbo.Customers (PublicId, Name, City) VALUES (@id, @n, N'Pune')", new[] { TestDb.P("@id", customer), TestDb.P("@n", "Cust " + Code()) });
        async Task<int> Default(string type) => await _fx.Db.ExecuteScalarAsync<int>("SELECT Id FROM dbo.MasterValues WHERE MasterType = @t AND IsDefault = 1", new[] { TestDb.P("@t", type) });

        var saleId = await _fx.Db.ExecuteProcedureScalarAsync<Guid>("dbo.usp_SaleOrder_Create", new[]
        {
            TestDb.P("@SalesUserId", seller), TestDb.P("@CustomerPublicId", customer), TestDb.P("@PlanId", plan), TestDb.P("@Quantity", 2),
            TestDb.P("@SaleAmount", 1800m), TestDb.P("@StartDate", DateTime.UtcNow.Date), TestDb.P("@GstPercent", 18m), TestDb.P("@FreeMonths", 1),
            TestDb.P("@BillingFrequencyId", await Default("BillingFrequency")), TestDb.P("@BillingMethodId", await Default("BillingMethod")),
            TestDb.P("@PaymentMethodId", await Default("PaymentMethod")), TestDb.P("@PaymentTermId", await Default("PaymentTerm")),
            TestDb.P("@CurrencyId", await Default("Currency")), TestDb.P("@PrimaryContactName", "Asha Rao"), TestDb.P("@BillToAddress", "Floor 2, Pune")
        });

        var sale = await Call(HttpMethod.Get, $"/api/admin/sales/{saleId}", token);
        sale.Status.ShouldBe(200, sale.Root.ToString());
        sale.Data.GetProperty("taxAmount").GetDecimal().ShouldBe(324m);
        sale.Data.GetProperty("totalAmount").GetDecimal().ShouldBe(2124m);
        sale.Data.GetProperty("freeMonths").GetInt32().ShouldBe(1);
        var order = sale.Data.GetProperty("orderDetails");
        order.GetProperty("billingFrequency").GetString().ShouldBe("Annual");
        order.GetProperty("currency").GetString().ShouldBe("INR");
        order.GetProperty("primaryContactName").GetString().ShouldBe("Asha Rao");
        order.GetProperty("billToAddress").GetString().ShouldBe("Floor 2, Pune");

        // The list carries the totals too; a simple sale has no order details.
        var list = await Call(HttpMethod.Get, $"/api/admin/sales?sellerId={await _fx.Db.ExecuteScalarAsync<Guid>("SELECT PublicId FROM dbo.SalesUsers WHERE Id = @id", new[] { TestDb.P("@id", seller) })}", token);
        list.Data.GetProperty("items")[0].GetProperty("totalAmount").GetDecimal().ShouldBe(2124m);
        list.Data.GetProperty("items")[0].GetProperty("orderDetails").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
