using System.Text.Json;
using SageUserApi.Tests.Infrastructure;
using Shouldly;

namespace SageUserApi.Tests.IntegrationTests;

/// <summary>
/// Lead Records: everyone sees every lead; the creator and the creator's manager change it (the company stays); managers
/// delete it (hidden); paging and filters come from dbo.usp_Lead_GetPaged; New Order links the sale to a Won lead.
/// </summary>
[Collection(UserApiFixture.CollectionName)]
public class LeadsApiTests
{
    private readonly UserApiFixture _fx;
    public LeadsApiTests(UserApiFixture fx) { _fx = fx; }

    private const string Api = "/api/user";
    private Task<ApiResult> Call(HttpMethod m, string url, object? body = null, string? bearer = null)
        => UserHttp.SendAsync(_fx.CreateClient(), m, url, body, bearer);
    private async Task<string> TokenAsync(SalesUserRow u) => (await UserHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;
    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    private async Task<Guid> CompanyAsync(string token, string name)
    {
        var res = await Call(HttpMethod.Post, $"{Api}/customers", new { name, city = "Pune", contactPerson = "Ms. Riya Patel", email = "riya@example.com", phone = "9876543210" }, token);
        res.Status.ShouldBe(201, res.Root.ToString());
        return res.Data.GetProperty("id").GetGuid();
    }

    private static int IdOf(JsonElement options, string list, string name)
        => options.GetProperty(list).EnumerateArray().Single(o => o.GetProperty("name").GetString() == name).GetProperty("id").GetInt32();

    private static Dictionary<string, object?> Lead(Guid company, SalesUserRow generatedBy, SalesUserRow assigned, JsonElement options, Action<Dictionary<string, object?>>? change = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["customerId"] = company, ["clientName"] = "Ms. Riya Patel", ["clientDesignation"] = "CTO", ["clientEmail"] = "riya@example.com",
            ["clientPhone"] = "+91 98765-43210", ["secondaryClientName"] = "Mr. Yash Jain",
            ["productId"] = IdOf(options, "products", "AZURE"), ["sourceId"] = IdOf(options, "sources", "Referral"),
            ["requirement"] = "Move 40 servers to Azure", ["statusId"] = IdOf(options, "statuses", "Open"), ["typeId"] = IdOf(options, "types", "Self Working"),
            ["generatedById"] = generatedBy.PublicId, ["assignedId"] = assigned.PublicId,
            ["monthlyExpectedRevenue"] = 65500m, ["billingCycleId"] = IdOf(options, "billingCycles", "Yearly"),
            ["leadDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"), ["remarks"] = "Call on Monday"
        };
        change?.Invoke(body);
        return body;
    }

    private async Task<JsonElement> ListAsync(string token, string query)
    {
        var res = await Call(HttpMethod.Get, $"{Api}/leads?{query}", bearer: token);
        res.Status.ShouldBe(200, res.Root.ToString());
        return res.Data;
    }

    [Fact]
    public async Task Options_HaveTheSeededLists_AndSalesUsers()
    {
        var token = await TokenAsync(await SalesData.CreateAsync(_fx.Db));
        var o = (await Call(HttpMethod.Get, $"{Api}/leads/options", bearer: token)).Data;
        string[] Names(string list) => o.GetProperty(list).EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToArray();
        Names("products").ShouldContain("LinkedIn Sales Nav");
        Names("products").Length.ShouldBeGreaterThanOrEqualTo(11);
        Names("sources").ShouldBe(new[] { "Cold Call", "Email Campaign", "Event", "LinkedIn", "Partner", "Referral", "Website" });
        Names("billingCycles").ShouldBe(new[] { "Monthly", "Quarterly", "Half Yearly", "Yearly" });
        Names("statuses").ShouldBe(new[] { "Open", "Warm", "Won", "Lost", "Hold", "Disqualified" });
        Names("types").ShouldBe(new[] { "Self Working", "Passed" });
        o.GetProperty("salesUsers").GetArrayLength().ShouldBeGreaterThan(0);
        (await Call(HttpMethod.Get, $"{Api}/leads/options")).Status.ShouldBe(401);
    }

    [Fact]
    public async Task EveryoneSeesALead_OnlyTheCreatorAndTheirManagerChangeIt_AndTheCompanyStays()
    {
        var manager = await SalesData.CreateAsync(_fx.Db);
        var person = await SalesData.CreateAsync(_fx.Db, "SalesPerson", manager);
        var otherManager = await SalesData.CreateAsync(_fx.Db);
        var other = await SalesData.CreateAsync(_fx.Db, "SalesPerson", otherManager);
        var pt = await TokenAsync(person);
        var options = (await Call(HttpMethod.Get, $"{Api}/leads/options", bearer: pt)).Data;
        var companyName = Unique("Juniper Infra ");
        var company = await CompanyAsync(pt, companyName);

        var created = await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, person, other, options), pt);
        created.Status.ShouldBe(201, created.Root.ToString());
        var lead = created.Data;
        var id = lead.GetProperty("id").GetGuid();
        lead.GetProperty("leadNumber").GetString()!.ShouldStartWith("LD-");
        lead.GetProperty("companyName").GetString().ShouldBe(companyName);
        lead.GetProperty("statusCode").GetString().ShouldBe("OPEN");
        lead.GetProperty("assignedName").GetString().ShouldNotBeNullOrEmpty();
        lead.GetProperty("canEdit").GetBoolean().ShouldBeTrue();
        lead.GetProperty("canDelete").GetBoolean().ShouldBeFalse();     // a sales person never deletes

        // Someone from another team sees it, but cannot change it.
        var ot = await TokenAsync(other);
        var seen = (await ListAsync(ot, $"search={Uri.EscapeDataString(companyName)}")).GetProperty("items").EnumerateArray().Single();
        seen.GetProperty("canEdit").GetBoolean().ShouldBeFalse();
        var refused = await Call(HttpMethod.Put, $"{Api}/leads/{id}", Lead(company, person, other, options), ot);
        refused.Status.ShouldBe(403);
        (await Call(HttpMethod.Put, $"{Api}/leads/{id}", Lead(company, person, other, options), await TokenAsync(otherManager))).Status.ShouldBe(403);

        // The creator's manager changes it; the company cannot be changed.
        var mt = await TokenAsync(manager);
        var edited = await Call(HttpMethod.Put, $"{Api}/leads/{id}", Lead(company, person, other, options, b =>
        {
            b["statusId"] = IdOf(options, "statuses", "Warm"); b["monthlyExpectedRevenue"] = 88000m; b["productId"] = IdOf(options, "products", "AWS");
        }), mt);
        edited.Status.ShouldBe(200, edited.Root.ToString());
        edited.Data.GetProperty("statusName").GetString().ShouldBe("Warm");
        edited.Data.GetProperty("productName").GetString().ShouldBe("AWS");
        edited.Data.GetProperty("monthlyExpectedRevenue").GetDecimal().ShouldBe(88000m);
        var otherCompany = await CompanyAsync(pt, Unique("Cedar Edu "));
        var moved = await Call(HttpMethod.Put, $"{Api}/leads/{id}", Lead(otherCompany, person, other, options), pt);
        moved.Status.ShouldBe(400);
        moved.FirstMessage.ShouldBe("The company of a lead cannot be changed.");
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE Action IN ('CreateLead', 'UpdateLead') AND EntityId = @id",
            new[] { TestDb.P("@id", id.ToString()) })).ShouldBe(2);

        // Delete: not a sales person, not another team's manager; the creator's manager hides it.
        (await Call(HttpMethod.Delete, $"{Api}/leads/{id}", bearer: pt)).Status.ShouldBe(403);
        (await Call(HttpMethod.Delete, $"{Api}/leads/{id}", bearer: await TokenAsync(otherManager))).Status.ShouldBe(403);
        (await Call(HttpMethod.Get, $"{Api}/leads/{id}", bearer: mt)).Data.GetProperty("canDelete").GetBoolean().ShouldBeTrue();
        (await Call(HttpMethod.Delete, $"{Api}/leads/{id}", bearer: mt)).Status.ShouldBe(200);
        (await Call(HttpMethod.Get, $"{Api}/leads/{id}", bearer: pt)).Status.ShouldBe(404);
        (await ListAsync(pt, $"search={Uri.EscapeDataString(companyName)}")).GetProperty("totalCount").GetInt32().ShouldBe(0);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Leads WHERE PublicId = @id AND IsDeleted = 1 AND DeletedBySalesUserId = @m",
            new[] { TestDb.P("@id", id), TestDb.P("@m", manager.Id) })).ShouldBe(1);
    }

    [Fact]
    public async Task WrongValues_AreRefused()
    {
        var manager = await SalesData.CreateAsync(_fx.Db);
        var inactive = await SalesData.CreateAsync(_fx.Db, "SalesPerson", manager, status: "Inactive");
        var mt = await TokenAsync(manager);
        var options = (await Call(HttpMethod.Get, $"{Api}/leads/options", bearer: mt)).Data;
        var company = await CompanyAsync(mt, Unique("Orbit Infra "));

        (await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, manager, manager, options, b => b["customerId"] = null), mt)).FirstMessage
            .ShouldBe("Choose the company, or add a new one.");
        (await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, manager, manager, options, b => b["clientEmail"] = "not-an-email"), mt)).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, manager, manager, options, b => b["clientPhone"] = "12345678901"), mt)).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, manager, manager, options, b => b["monthlyExpectedRevenue"] = -1m), mt)).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, manager, manager, options, b => b["leadDate"] = null), mt)).Status.ShouldBe(400);
        var person = await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, inactive, manager, options), mt);
        person.Status.ShouldBe(400);
        person.FirstMessage.ShouldContain("active sales users");
        var wrongList = await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, manager, manager, options, b => b["productId"] = 999999), mt);
        wrongList.FirstMessage.ShouldBe("Choose a product from the list");
    }

    [Fact]
    public async Task List_IsPagedAndFilteredByTheDatabase()
    {
        var manager = await SalesData.CreateAsync(_fx.Db);
        var person = await SalesData.CreateAsync(_fx.Db, "SalesPerson", manager);
        var mt = await TokenAsync(manager);
        var options = (await Call(HttpMethod.Get, $"{Api}/leads/options", bearer: mt)).Data;
        var tag = Unique("Glacier ");
        var company = await CompanyAsync(mt, tag + " Foods");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));
        async Task Add(string status, string product, int daysAgo, SalesUserRow assigned)
        {
            var res = await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, manager, assigned, options, b =>
            {
                b["statusId"] = IdOf(options, "statuses", status); b["productId"] = IdOf(options, "products", product);
                b["leadDate"] = today.AddDays(-daysAgo).ToString("yyyy-MM-dd");
            }), mt);
            res.Status.ShouldBe(201, res.Root.ToString());
        }
        await Add("Open", "AWS", 2, person);
        await Add("Won", "AZURE", 10, manager);
        await Add("Lost", "AWS", 40, person);
        await Add("Open", "DLP", 90, manager);
        await Add("Hold", "AWS", 5, person);

        string s = "search=" + Uri.EscapeDataString(tag);
        var page1 = await ListAsync(mt, $"{s}&pageSize=2&page=1");
        page1.GetProperty("totalCount").GetInt32().ShouldBe(5);
        page1.GetProperty("totalPages").GetInt32().ShouldBe(3);
        page1.GetProperty("items").GetArrayLength().ShouldBe(2);
        var page3 = await ListAsync(mt, $"{s}&pageSize=2&page=3");
        page3.GetProperty("items").GetArrayLength().ShouldBe(1);

        (await ListAsync(mt, $"{s}&statusId={IdOf(options, "statuses", "Open")}")).GetProperty("totalCount").GetInt32().ShouldBe(2);
        (await ListAsync(mt, $"{s}&productId={IdOf(options, "products", "AWS")}")).GetProperty("totalCount").GetInt32().ShouldBe(3);
        (await ListAsync(mt, $"{s}&assignedId={person.PublicId}")).GetProperty("totalCount").GetInt32().ShouldBe(3);
        (await ListAsync(mt, $"{s}&generatedById={manager.PublicId}")).GetProperty("totalCount").GetInt32().ShouldBe(5);
        (await ListAsync(mt, $"{s}&aging=lt7")).GetProperty("totalCount").GetInt32().ShouldBe(2);       // 2 and 5 days
        (await ListAsync(mt, $"{s}&aging=7to30")).GetProperty("totalCount").GetInt32().ShouldBe(1);
        (await ListAsync(mt, $"{s}&aging=31to60")).GetProperty("totalCount").GetInt32().ShouldBe(1);
        var old = (await ListAsync(mt, $"{s}&aging=gt60")).GetProperty("items")[0];
        old.GetProperty("agingDays").GetInt32().ShouldBe(90);
        (await ListAsync(mt, $"{s}&from={today.AddDays(-12):yyyy-MM-dd}&to={today.AddDays(-3):yyyy-MM-dd}")).GetProperty("totalCount").GetInt32().ShouldBe(2);
        // Newest first by default.
        var first = page1.GetProperty("items")[0].GetProperty("leadNumber").GetString()!;
        var second = page1.GetProperty("items")[1].GetProperty("leadNumber").GetString()!;
        int.Parse(first[3..]).ShouldBeGreaterThan(int.Parse(second[3..]));
    }

    [Fact]
    public async Task Summary_CountsMyLeads_ByStatusSourceAgingAndType_WithWonRevenue()
    {
        var me = await SalesData.CreateAsync(_fx.Db);
        var mt = await TokenAsync(me);
        var options = (await Call(HttpMethod.Get, $"{Api}/leads/options", bearer: mt)).Data;
        var company = await CompanyAsync(mt, Unique("Aster Energy "));
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));
        async Task Add(string status, string product, string source, string type, decimal revenue, int daysAgo)
        {
            var res = await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, me, me, options, b =>
            {
                b["statusId"] = IdOf(options, "statuses", status); b["productId"] = IdOf(options, "products", product);
                b["sourceId"] = IdOf(options, "sources", source); b["typeId"] = IdOf(options, "types", type);
                b["monthlyExpectedRevenue"] = revenue; b["leadDate"] = today.AddDays(-daysAgo).ToString("yyyy-MM-dd");
            }), mt);
            res.Status.ShouldBe(201, res.Root.ToString());
        }
        await Add("Won", "AWS", "Referral", "Self Working", 100000m, 2);
        await Add("Won", "AZURE", "Referral", "Passed", 50000m, 10);
        await Add("Open", "AWS", "Website", "Self Working", 70000m, 40);
        await Add("Lost", "DLP", "Website", "Passed", 30000m, 90);

        var res = await Call(HttpMethod.Get, $"{Api}/leads/summary?mine=true", bearer: mt);
        res.Status.ShouldBe(200, res.Root.ToString());
        var s = res.Data;
        s.GetProperty("totalLeads").GetInt32().ShouldBe(4);
        s.GetProperty("wonLeads").GetInt32().ShouldBe(2);
        s.GetProperty("conversionRate").GetDecimal().ShouldBe(50m);
        s.GetProperty("wonRevenueMonthly").GetDecimal().ShouldBe(150000m);          // only Won leads count
        s.GetProperty("wonRevenueAnnualized").GetDecimal().ShouldBe(1800000m);
        int Count(string section, string name) => s.GetProperty(section).EnumerateArray().Single(x => x.GetProperty("name").GetString() == name).GetProperty("total").GetInt32();
        Count("statuses", "Won").ShouldBe(2);
        Count("statuses", "Hold").ShouldBe(0);                                         // every status is listed
        Count("sources", "Referral").ShouldBe(2);
        Count("aging", "< 7 days").ShouldBe(1);
        Count("aging", "7-30 days").ShouldBe(1);
        Count("aging", "31-60 days").ShouldBe(1);
        Count("aging", "> 60 days").ShouldBe(1);
        var passed = s.GetProperty("types").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "Passed");
        passed.GetProperty("total").GetInt32().ShouldBe(2);
        passed.GetProperty("won").GetInt32().ShouldBe(1);
        var aws = s.GetProperty("products").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "AWS");
        aws.GetProperty("won").GetInt32().ShouldBe(1);
        aws.GetProperty("revenue").GetDecimal().ShouldBe(100000m);
        // All leads includes everyone's.
        (await Call(HttpMethod.Get, $"{Api}/leads/summary", bearer: mt)).Data.GetProperty("totalLeads").GetInt32().ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task OrderWithNoLead_CreatesAnOpenLeadForTheCompany_AndLinksIt()
    {
        var person = await SalesData.CreateAsync(_fx.Db, "SalesPerson", await SalesData.CreateAsync(_fx.Db));
        var pt = await TokenAsync(person);
        var companyName = Unique("Nova Industries ");
        var company = await CompanyAsync(pt, companyName);
        var planId = await PlanIdAsync();
        var of = (await Call(HttpMethod.Get, $"{Api}/order-form/options", bearer: pt)).Data;
        int Default(string list) => of.GetProperty(list).EnumerateArray().Single(o => o.GetProperty("isDefault").GetBoolean()).GetProperty("id").GetInt32();
        object OrderBody(Guid? leadId) => new
        {
            customerId = company, planId, quantity = 1, discountAmount = 0m, startDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), gstPercent = 18m, freeMonths = 0,
            billingFrequencyId = Default("billingFrequencies"), billingMethodId = Default("billingMethods"), paymentMethodId = Default("paymentMethods"),
            paymentTermId = Default("paymentTerms"), currencyId = Default("currencies"),
            primaryContact = new { name = "Ms. Aditya Pillai", designation = "CFO", email = "aditya@example.com", phone = "9876501234" },
            billingSameAsPrimary = true, leadId
        };

        var order = await Call(HttpMethod.Post, $"{Api}/order-form", OrderBody(null), pt);
        order.Status.ShouldBe(201, order.Root.ToString());
        var leadNumber = order.Data.GetProperty("leadNumber").GetString()!;
        leadNumber.ShouldStartWith("LD-");

        var leads = (await ListAsync(pt, $"search={Uri.EscapeDataString(companyName)}")).GetProperty("items");
        leads.GetArrayLength().ShouldBe(1);
        var lead = leads[0];
        lead.GetProperty("leadNumber").GetString().ShouldBe(leadNumber);
        lead.GetProperty("statusCode").GetString().ShouldBe("OPEN");
        lead.GetProperty("typeName").GetString().ShouldBe("Self Working");
        lead.GetProperty("clientName").GetString().ShouldBe("Ms. Aditya Pillai");
        lead.GetProperty("clientEmail").GetString().ShouldBe("aditya@example.com");
        lead.GetProperty("generatedById").GetGuid().ShouldBe(person.PublicId);
        lead.GetProperty("assignedId").GetGuid().ShouldBe(person.PublicId);
        lead.GetProperty("saleNumber").GetString().ShouldBe(order.Data.GetProperty("saleNumber").GetString());
        lead.GetProperty("canEdit").GetBoolean().ShouldBeTrue();          // the rest is filled in later

        // An order made from a lead does not create another one.
        var picked = await Call(HttpMethod.Post, $"{Api}/order-form", OrderBody(
            (await Call(HttpMethod.Post, $"{Api}/leads", new
            {
                customerId = company, statusId = 1, typeId = 1, generatedById = person.PublicId, assignedId = person.PublicId,
                leadDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
            }, pt)).Data.GetProperty("id").GetGuid()), pt);
        picked.Status.ShouldBe(201, picked.Root.ToString());
        (await ListAsync(pt, $"search={Uri.EscapeDataString(companyName)}")).GetProperty("totalCount").GetInt32().ShouldBe(2);
    }

    private async Task<long> PlanIdAsync()
    {
        var categoryId = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductCategories (Code, Name) OUTPUT INSERTED.Id VALUES (@c, @n)",
            new[] { TestDb.P("@c", Unique("Cat")), TestDb.P("@n", Unique("Category ")) });
        var productId = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.Products (CategoryId, Code, Name) OUTPUT INSERTED.Id VALUES (@cat, @c, @n)",
            new[] { TestDb.P("@cat", categoryId), TestDb.P("@c", Unique("Prd")), TestDb.P("@n", Unique("Product ")) });
        return await _fx.Db.ExecuteScalarAsync<long>(
            "INSERT dbo.ProductPlans (ProductId, Name, DurationUnit, DurationCount, Price, Cost) OUTPUT INSERTED.Id VALUES (@p, N'Yearly', 'Year', 1, 1000, 600)",
            new[] { TestDb.P("@p", productId) });
    }

    [Fact]
    public async Task NewOrder_FromAWonLead_LinksTheOrderToTheLead()
    {
        var manager = await SalesData.CreateAsync(_fx.Db);
        var person = await SalesData.CreateAsync(_fx.Db, "SalesPerson", manager);
        var assigned = await SalesData.CreateAsync(_fx.Db, "SalesPerson", await SalesData.CreateAsync(_fx.Db));
        var pt = await TokenAsync(person);
        var options = (await Call(HttpMethod.Get, $"{Api}/leads/options", bearer: pt)).Data;
        var company = await CompanyAsync(pt, Unique("Willow Media "));
        var open = await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, person, assigned, options), pt);
        var id = open.Data.GetProperty("id").GetGuid();
        open.Data.GetProperty("canOrder").GetBoolean().ShouldBeFalse();     // not Won yet

        var won = await Call(HttpMethod.Put, $"{Api}/leads/{id}", Lead(company, person, assigned, options, b => b["statusId"] = IdOf(options, "statuses", "Won")), pt);
        won.Data.GetProperty("canOrder").GetBoolean().ShouldBeTrue();
        // The assigned person (another team) may make the order, though they cannot edit the lead.
        var at = await TokenAsync(assigned);
        var forAssigned = (await Call(HttpMethod.Get, $"{Api}/leads/{id}", bearer: at)).Data;
        forAssigned.GetProperty("canOrder").GetBoolean().ShouldBeTrue();
        forAssigned.GetProperty("canEdit").GetBoolean().ShouldBeFalse();

        // An order saved with leadId is linked to the lead.
        var categoryId = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.ProductCategories (Code, Name) OUTPUT INSERTED.Id VALUES (@c, @n)",
            new[] { TestDb.P("@c", Unique("Cat")), TestDb.P("@n", Unique("Category ")) });
        var productId = await _fx.Db.ExecuteScalarAsync<long>("INSERT dbo.Products (CategoryId, Code, Name) OUTPUT INSERTED.Id VALUES (@cat, @c, @n)",
            new[] { TestDb.P("@cat", categoryId), TestDb.P("@c", Unique("Prd")), TestDb.P("@n", Unique("Product ")) });
        var planId = await _fx.Db.ExecuteScalarAsync<long>(
            "INSERT dbo.ProductPlans (ProductId, Name, DurationUnit, DurationCount, Price, Cost) OUTPUT INSERTED.Id VALUES (@p, N'Yearly', 'Year', 1, 1000, 600)",
            new[] { TestDb.P("@p", productId) });
        var of = (await Call(HttpMethod.Get, $"{Api}/order-form/options", bearer: at)).Data;
        int Default(string list) => of.GetProperty(list).EnumerateArray().Single(o => o.GetProperty("isDefault").GetBoolean()).GetProperty("id").GetInt32();
        var order = await Call(HttpMethod.Post, $"{Api}/order-form", new
        {
            customerId = company, planId, quantity = 1, discountAmount = 0m, startDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), gstPercent = 18m, freeMonths = 0,
            billingFrequencyId = Default("billingFrequencies"), billingMethodId = Default("billingMethods"), paymentMethodId = Default("paymentMethods"),
            paymentTermId = Default("paymentTerms"), currencyId = Default("currencies"),
            primaryContact = new { name = "Ms. Riya Patel" }, billingSameAsPrimary = true, leadId = id
        }, at);
        order.Status.ShouldBe(201, order.Root.ToString());
        var linked = (await Call(HttpMethod.Get, $"{Api}/leads/{id}", bearer: pt)).Data;
        linked.GetProperty("saleNumber").GetString().ShouldBe(order.Data.GetProperty("saleNumber").GetString());
        linked.GetProperty("canOrder").GetBoolean().ShouldBeFalse();
        // The order form's lead picker only offers leads with no order yet; the Order forms list shows the lead.
        var leadNumber = linked.GetProperty("leadNumber").GetString()!;
        (await ListAsync(pt, $"search={leadNumber}&withoutOrder=true")).GetProperty("totalCount").GetInt32().ShouldBe(0);
        (await ListAsync(pt, $"search={leadNumber}")).GetProperty("totalCount").GetInt32().ShouldBe(1);
        (await Call(HttpMethod.Get, $"{Api}/sales?scope=mine&search={leadNumber}", bearer: at)).Data.GetProperty("items")[0]
            .GetProperty("leadNumber").GetString().ShouldBe(leadNumber);

        // An order can also be made from a lead that is not Won yet (picked in the order form's Lead box).
        var openLead = (await Call(HttpMethod.Post, $"{Api}/leads", Lead(company, person, assigned, options), pt)).Data.GetProperty("id").GetGuid();
        var second = await Call(HttpMethod.Post, $"{Api}/order-form", new
        {
            customerId = company, planId, quantity = 1, discountAmount = 0m, startDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), gstPercent = 18m, freeMonths = 0,
            billingFrequencyId = Default("billingFrequencies"), billingMethodId = Default("billingMethods"), paymentMethodId = Default("paymentMethods"),
            paymentTermId = Default("paymentTerms"), currencyId = Default("currencies"),
            primaryContact = new { name = "Ms. Riya Patel" }, billingSameAsPrimary = true, leadId = openLead
        }, pt);
        second.Status.ShouldBe(201, second.Root.ToString());
        (await Call(HttpMethod.Get, $"{Api}/leads/{openLead}", bearer: pt)).Data.GetProperty("saleNumber").GetString()
            .ShouldBe(second.Data.GetProperty("saleNumber").GetString());
    }
}
