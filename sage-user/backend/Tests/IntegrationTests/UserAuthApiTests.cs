using SageUserApi.Tests.Infrastructure;
using Shouldly;

namespace SageUserApi.Tests.IntegrationTests;

[Collection(UserApiFixture.CollectionName)]
public class UserAuthApiTests
{
    private readonly UserApiFixture _fx;
    public UserAuthApiTests(UserApiFixture fx) { _fx = fx; }

    private const string Auth = "/api/user/auth";
    private Task<ApiResult> Call(HttpMethod m, string url, object? body = null, string? bearer = null, string? cookie = null)
        => UserHttp.SendAsync(_fx.CreateClient(), m, url, body, bearer, cookie);
    private Task<ApiResult> Login(SalesUserRow u, string? password = null) => UserHttp.LoginAsync(_fx.CreateClient(), u.Username, password ?? u.Password);

    // ---------------- sign-in ----------------

    [Fact]
    public async Task Login_ReturnsTokenAndUser_SetsHttpOnlyCookie_AndMeWorks()
    {
        var manager = await SalesData.CreateAsync(_fx.Db);
        var person = await SalesData.CreateAsync(_fx.Db, "SalesPerson", manager);

        var res = await Login(person);
        res.Status.ShouldBe(200);
        res.AccessToken.ShouldNotBeNullOrEmpty();
        res.Data.GetProperty("user").GetProperty("role").GetString().ShouldBe("SalesPerson");
        res.Data.GetProperty("user").GetProperty("isManager").GetBoolean().ShouldBeFalse();
        res.Data.GetProperty("user").GetProperty("managerName").GetString().ShouldBe("Sam Seller");
        res.Data.TryGetProperty("refreshToken", out _).ShouldBeFalse();
        res.RefreshCookie.ShouldNotBeNullOrEmpty();
        res.Response.Headers.GetValues("Set-Cookie").First().ToLowerInvariant().ShouldContain("httponly");

        var me = await Call(HttpMethod.Get, $"{Auth}/me", bearer: res.AccessToken);
        me.Status.ShouldBe(200);
        me.Data.GetProperty("username").GetString().ShouldBe(person.Username);

        var managerMe = await Call(HttpMethod.Get, $"{Auth}/me", bearer: (await Login(manager)).AccessToken);
        managerMe.Data.GetProperty("isManager").GetBoolean().ShouldBeTrue();
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SalesUserLoginHistories WHERE SalesUserId = @id AND IsSuccess = 1",
            new[] { TestDb.P("@id", person.Id) })).ShouldBe(1);
    }

    [Fact]
    public async Task Login_ByEmail_Works_AndBadCredentialsGetTheSameAnswer()
    {
        var u = await SalesData.CreateAsync(_fx.Db);
        (await UserHttp.LoginAsync(_fx.CreateClient(), $"{u.Username}@sales.example.com", u.Password)).Status.ShouldBe(200);

        var wrong = await Login(u, "Wrong#Passw0rd");
        var unknown = await UserHttp.LoginAsync(_fx.CreateClient(), "nobody_" + Guid.NewGuid().ToString("N")[..8], "Wrong#Passw0rd");
        wrong.Status.ShouldBe(401);
        unknown.Status.ShouldBe(401);
        wrong.FirstMessage.ShouldBe(unknown.FirstMessage);
        (await UserHttp.LoginAsync(_fx.CreateClient(), "", "")).Status.ShouldBe(400);
    }

    [Fact]
    public async Task InactiveUser_CannotSignIn()
    {
        var u = await SalesData.CreateAsync(_fx.Db, status: "Inactive");
        (await Login(u)).Status.ShouldBe(401);
    }

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccount_EvenForTheRightPassword()
    {
        var u = await SalesData.CreateAsync(_fx.Db);
        for (var i = 0; i < 5; i++) (await Login(u, "Wrong#Passw0rd")).Status.ShouldBe(401);
        var locked = await Login(u);
        locked.Status.ShouldBe(429);
        locked.FirstMessage.ShouldContain("locked");
    }

    [Fact]
    public async Task AdminApiUsers_CannotSignInHere()
    {
        // Admin accounts live in dbo.AdminUsers; the User Panel only knows dbo.SalesUsers.
        await _fx.Db.ExecuteNonQueryAsync("INSERT dbo.AdminUsers (FirstName, LastName, Username, Email, PasswordHash) VALUES (N'A', N'B', N'only_admin_x', N'only_admin_x@example.com', N'x')");
        (await UserHttp.LoginAsync(_fx.CreateClient(), "only_admin_x", SalesData.Password)).Status.ShouldBe(401);
    }

    // ---------------- tokens ----------------

    [Fact]
    public async Task TokensFromAnotherIssuerOrKey_AreRefused()
    {
        var u = await SalesData.CreateAsync(_fx.Db);
        var pv = 1;
        (await Call(HttpMethod.Get, $"{Auth}/me", bearer: UserHttp.ForgeToken(u.PublicId, pv, UserApiFixture.JwtKey, UserApiFixture.Issuer, UserApiFixture.Audience))).Status.ShouldBe(200);
        // Admin API issuer/audience, even with this API's key, is refused...
        (await Call(HttpMethod.Get, $"{Auth}/me", bearer: UserHttp.ForgeToken(u.PublicId, pv, UserApiFixture.JwtKey, "sage.admin", "sage.admin.app"))).Status.ShouldBe(401);
        // ...and so is the right issuer signed with another key.
        (await Call(HttpMethod.Get, $"{Auth}/me", bearer: UserHttp.ForgeToken(u.PublicId, pv, "some-other-signing-key-0123456789-abcdef", UserApiFixture.Issuer, UserApiFixture.Audience))).Status.ShouldBe(401);
        (await Call(HttpMethod.Get, $"{Auth}/me")).Status.ShouldBe(401);
    }

    [Fact]
    public async Task Refresh_RotatesTheCookie_AndReplayingAnOldOneEndsTheSession()
    {
        var u = await SalesData.CreateAsync(_fx.Db);
        var first = (await Login(u)).RefreshCookie;

        var refreshed = await Call(HttpMethod.Post, $"{Auth}/refresh-token", cookie: first);
        refreshed.Status.ShouldBe(200);
        refreshed.AccessToken.ShouldNotBeNullOrEmpty();
        var second = refreshed.RefreshCookie;
        second.ShouldNotBe(first);

        (await Call(HttpMethod.Post, $"{Auth}/refresh-token", cookie: first)).Status.ShouldBe(401);   // replay
        (await Call(HttpMethod.Post, $"{Auth}/refresh-token", cookie: second)).Status.ShouldBe(401);  // whole family revoked
        (await Call(HttpMethod.Post, $"{Auth}/refresh-token")).Status.ShouldBe(401);
    }

    [Fact]
    public async Task Logout_EndsTheSession_AndIsIdempotent()
    {
        var u = await SalesData.CreateAsync(_fx.Db);
        var cookie = (await Login(u)).RefreshCookie;
        (await Call(HttpMethod.Post, $"{Auth}/logout", cookie: cookie)).Status.ShouldBe(200);
        (await Call(HttpMethod.Post, $"{Auth}/refresh-token", cookie: cookie)).Status.ShouldBe(401);
        (await Call(HttpMethod.Post, $"{Auth}/logout")).Status.ShouldBe(200);
    }

    // ---------------- password ----------------

    [Fact]
    public async Task ChangePassword_EndsAllSessions_ClearsMustChange_AndNewPasswordWorks()
    {
        var u = await SalesData.CreateAsync(_fx.Db, mustChangePassword: true);
        var login = await Login(u);
        login.Data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        var token = login.AccessToken;

        (await Call(HttpMethod.Post, $"{Auth}/change-password", new { currentPassword = "Wrong#Passw0rd", newPassword = "New#Passw0rd99", confirmPassword = "New#Passw0rd99" }, token)).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Auth}/change-password", new { currentPassword = u.Password, newPassword = "weak", confirmPassword = "weak" }, token)).Status.ShouldBe(400);
        (await Call(HttpMethod.Post, $"{Auth}/change-password", new { currentPassword = u.Password, newPassword = u.Password, confirmPassword = u.Password }, token)).Status.ShouldBe(400);

        var ok = await Call(HttpMethod.Post, $"{Auth}/change-password", new { currentPassword = u.Password, newPassword = "New#Passw0rd99", confirmPassword = "New#Passw0rd99" }, token);
        ok.Status.ShouldBe(200);

        (await Call(HttpMethod.Get, $"{Auth}/me", bearer: token)).Status.ShouldBe(401);
        (await Call(HttpMethod.Post, $"{Auth}/refresh-token", cookie: login.RefreshCookie)).Status.ShouldBe(401);
        (await Login(u)).Status.ShouldBe(401);
        var again = await Login(u, "New#Passw0rd99");
        again.Status.ShouldBe(200);
        again.Data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();
    }

    // ---------------- admin actions take effect here ----------------

    [Fact]
    public async Task AdminDeactivation_EndsUserPanelSessions_Immediately()
    {
        var u = await SalesData.CreateAsync(_fx.Db);
        var login = await Login(u);
        await _fx.Db.ExecuteNonQueryAsync("EXEC dbo.usp_SalesUser_SetStatus @PublicId = @p, @Status = 'Inactive'", new[] { TestDb.P("@p", u.PublicId) });

        (await Call(HttpMethod.Get, $"{Auth}/me", bearer: login.AccessToken)).Status.ShouldBe(401);
        (await Call(HttpMethod.Post, $"{Auth}/refresh-token", cookie: login.RefreshCookie)).Status.ShouldBe(401);
        (await Login(u)).Status.ShouldBe(401);
    }

    [Fact]
    public async Task AdminPasswordReset_EndsSessions_AndForcesAChange()
    {
        var u = await SalesData.CreateAsync(_fx.Db);
        var login = await Login(u);
        var newHash = SageCommonLib.Security.SecurityHash.HashPassword("Admin#Set0123", 1000);
        await _fx.Db.ExecuteNonQueryAsync("EXEC dbo.usp_SalesUser_ResetPassword @PublicId = @p, @PasswordHash = @h", new[] { TestDb.P("@p", u.PublicId), TestDb.P("@h", newHash) });

        (await Call(HttpMethod.Post, $"{Auth}/refresh-token", cookie: login.RefreshCookie)).Status.ShouldBe(401);
        var fresh = await Login(u, "Admin#Set0123");
        fresh.Status.ShouldBe(200);
        fresh.Data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    // ---------------- misc ----------------

    [Fact]
    public async Task Status_IsAnonymous_AndUnknownRoutesNeedSignIn()
    {
        var status = await Call(HttpMethod.Get, "/api/user/status");
        status.Status.ShouldBe(200);
        status.Data.GetProperty("database").GetString().ShouldBe("Up");
        (await Call(HttpMethod.Get, "/api/user/anything")).Status.ShouldBe(401);
    }
}
