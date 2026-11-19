using SageUserApi.Tests.Infrastructure;
using Shouldly;

namespace SageUserApi.Tests.IntegrationTests;

/// <summary>A sales manager adds a sales person to their own team from the User Panel.</summary>
[Collection(UserApiFixture.CollectionName)]
public class TeamMembersApiTests
{
    private readonly UserApiFixture _fx;
    public TeamMembersApiTests(UserApiFixture fx) { _fx = fx; }

    private const string Url = "/api/user/sales/team-members";
    private Task<ApiResult> Call(HttpMethod m, string url, object? body = null, string? bearer = null)
        => UserHttp.SendAsync(_fx.CreateClient(), m, url, body, bearer);
    private async Task<string> TokenAsync(SalesUserRow u) => (await UserHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;
    private static string Unique() => ("tm" + Guid.NewGuid().ToString("N"))[..14];

    private static object Member(string username, string password = "Temp#Pass1234", string? confirm = null) => new
    {
        firstName = "Neha", lastName = "Verma", username, email = username + "@team.example.com", mobileNumber = "98765 43210",
        password, confirmPassword = confirm ?? password
    };

    [Fact]
    public async Task Manager_AddsASalesPerson_WhoJoinsTheTeam_AndMustChangeThePassword()
    {
        var manager = await SalesData.CreateAsync(_fx.Db);
        var token = await TokenAsync(manager);
        string username = Unique();

        var created = await Call(HttpMethod.Post, Url, Member(username), token);
        created.Status.ShouldBe(201, created.Root.ToString());
        created.FirstMessage.ShouldBe("Neha Verma added to your team.");
        created.Data.GetProperty("username").GetString().ShouldBe(username);
        created.Data.GetProperty("status").GetString().ShouldBe("Active");

        // In the manager's team, as a sales person reporting to them.
        var team = await Call(HttpMethod.Get, Url, bearer: token);
        team.Data.EnumerateArray().ShouldContain(m => m.GetProperty("username").GetString() == username);
        var row = (await _fx.Db.QueryAsync("SELECT Role, ManagerId, MustChangePassword FROM dbo.SalesUsers WHERE Username = @u",
            r => (Role: r.GetString(0), ManagerId: r.GetInt64(1), MustChange: r.GetBoolean(2)), new[] { TestDb.P("@u", username) })).Single();
        row.Role.ShouldBe("SalesPerson");
        row.ManagerId.ShouldBe(manager.Id);
        row.MustChange.ShouldBeTrue();

        // The new person signs in with the temporary password and is asked to change it.
        var login = await UserHttp.LoginAsync(_fx.CreateClient(), username, "Temp#Pass1234");
        login.Status.ShouldBe(200, login.Root.ToString());
        login.Data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Manager_DeactivatesAndActivates_OwnTeamMember_Only()
    {
        var manager = await SalesData.CreateAsync(_fx.Db);
        var person = await SalesData.CreateAsync(_fx.Db, "SalesPerson", manager);
        var token = await TokenAsync(manager);
        string Status(Guid id) => $"{Url}/{id}/status";

        // Deactivated: signed out and cannot sign in.
        var off = await Call(HttpMethod.Put, Status(person.PublicId), new { status = "Inactive" }, token);
        off.Status.ShouldBe(200, off.Root.ToString());
        off.Data.GetProperty("status").GetString().ShouldBe("Inactive");
        (await UserHttp.LoginAsync(_fx.CreateClient(), person.Username, person.Password)).Status.ShouldNotBe(200);

        // Active again: can sign in.
        var on = await Call(HttpMethod.Put, Status(person.PublicId), new { status = "Active" }, token);
        on.Status.ShouldBe(200, on.Root.ToString());
        (await UserHttp.LoginAsync(_fx.CreateClient(), person.Username, person.Password)).Status.ShouldBe(200);

        // Logged for the Admin Panel's history (by the manager, in the User Panel).
        var log = await _fx.Db.QueryAsync("SELECT NewValues FROM dbo.AuditLogs WHERE Action = 'TeamMemberStatus' AND EntityId = @id ORDER BY Id",
            r => r.GetString(0), new[] { TestDb.P("@id", person.PublicId.ToString()) });
        log.Count.ShouldBe(2);
        log[0].ShouldContain("\"To\":\"Inactive\"");

        // Not someone else's team member, not a bad status, not by a sales person.
        var other = await SalesData.CreateAsync(_fx.Db, "SalesPerson");
        (await Call(HttpMethod.Put, Status(other.PublicId), new { status = "Inactive" }, token)).Status.ShouldBe(404);
        (await Call(HttpMethod.Put, Status(person.PublicId), new { status = "Gone" }, token)).Status.ShouldBe(400);
        (await Call(HttpMethod.Put, Status(person.PublicId), new { status = "Inactive" }, await TokenAsync(person))).Status.ShouldBe(403);
    }

    [Fact]
    public async Task OnlyManagers_CanAdd_AndTheRulesAreChecked()
    {
        var manager = await SalesData.CreateAsync(_fx.Db);
        var person = await SalesData.CreateAsync(_fx.Db, "SalesPerson", manager);
        (await Call(HttpMethod.Post, Url, Member(Unique()), await TokenAsync(person))).Status.ShouldBe(403);
        (await Call(HttpMethod.Post, Url, Member(Unique()))).Status.ShouldBe(401);

        var token = await TokenAsync(manager);
        (await Call(HttpMethod.Post, Url, Member("x"), token)).Status.ShouldBe(400);                              // username too short
        (await Call(HttpMethod.Post, Url, Member(Unique(), "weak"), token)).Status.ShouldBe(400);                 // weak password
        (await Call(HttpMethod.Post, Url, Member(Unique(), "Temp#Pass1234", "Other#Pass1234"), token)).Status.ShouldBe(400);

        // Username already taken (here: the manager's own).
        var taken = await Call(HttpMethod.Post, Url, Member(manager.Username), token);
        taken.Status.ShouldBe(409, taken.Root.ToString());
    }
}
