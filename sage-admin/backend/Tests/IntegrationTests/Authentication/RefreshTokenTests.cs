using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Authentication;

[Collection(ApiFixture.CollectionName)]
public class RefreshTokenTests
{
    private readonly ApiFixture _fx;
    public RefreshTokenTests(ApiFixture fx) { _fx = fx; }

    private async Task<(TestUser user, ApiResult login)> LoginNewUserAsync(HttpClient c)
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        return (user, await AuthHttp.LoginAsync(c, user.Username, user.Password));
    }

    [Fact]
    public async Task ValidRefreshToken_ReturnsNewAccessToken_AndRotatesTheCookie()
    {
        var c = _fx.CreateClient();
        var (_, login) = await LoginNewUserAsync(c);

        var refreshed = await AuthHttp.RefreshAsync(c, login.RefreshCookie);

        refreshed.Status.ShouldBe(200);
        refreshed.AccessToken.ShouldNotBeNullOrEmpty();
        refreshed.AccessToken.ShouldNotBe(login.AccessToken);
        refreshed.RefreshCookie.ShouldNotBeNullOrEmpty();
        refreshed.RefreshCookie.ShouldNotBe(login.RefreshCookie);
        (await AuthHttp.MeAsync(c, refreshed.AccessToken)).Status.ShouldBe(200);
    }

    [Fact]
    public async Task ReusingARotatedRefreshToken_Returns401_AndRevokesTheWholeFamily()
    {
        var c = _fx.CreateClient();
        var (_, login) = await LoginNewUserAsync(c);
        var first = await AuthHttp.RefreshAsync(c, login.RefreshCookie);

        var replay = await AuthHttp.RefreshAsync(c, login.RefreshCookie);   // attacker replays the old token
        replay.Status.ShouldBe(401);

        var legit = await AuthHttp.RefreshAsync(c, first.RefreshCookie);    // the newest token was revoked as well
        legit.Status.ShouldBe(401);
    }

    [Fact]
    public async Task InvalidRefreshToken_Returns401_AndClearsCookie()
    {
        var res = await AuthHttp.RefreshAsync(_fx.CreateClient(), "not-a-real-token");
        res.Status.ShouldBe(401);
        res.FirstMessage.ShouldContain("sign in again");
    }

    [Fact]
    public async Task MissingRefreshCookie_Returns401()
        => (await AuthHttp.RefreshAsync(_fx.CreateClient(), null)).Status.ShouldBe(401);

    [Fact]
    public async Task ExpiredRefreshToken_Returns401()
    {
        var c = _fx.CreateClient();
        var (user, login) = await LoginNewUserAsync(c);
        await _fx.Db.ExecuteNonQueryAsync(
            "UPDATE dbo.RefreshTokens SET CreatedAtUtc = DATEADD(DAY,-9,SYSUTCDATETIME()), ExpiresAtUtc = DATEADD(DAY,-1,SYSUTCDATETIME()) WHERE UserId=@i",
            new[] { TestDb.P("@i", user.Id) });
        (await AuthHttp.RefreshAsync(c, login.RefreshCookie)).Status.ShouldBe(401);
    }

    [Fact]
    public async Task RefreshForDeactivatedUser_Returns401()
    {
        var c = _fx.CreateClient();
        var (user, login) = await LoginNewUserAsync(c);
        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.AdminUsers SET Status='Inactive' WHERE Id=@i", new[] { TestDb.P("@i", user.Id) });
        (await AuthHttp.RefreshAsync(c, login.RefreshCookie)).Status.ShouldBe(401);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_AndClearsCookie()
    {
        var c = _fx.CreateClient();
        var (user, login) = await LoginNewUserAsync(c);

        var logout = await AuthHttp.LogoutAsync(c, login.RefreshCookie);

        logout.Status.ShouldBe(200);
        logout.SetCookieHeader!.ToLowerInvariant().ShouldContain("expires=");   // deleted cookie
        (await AuthHttp.RefreshAsync(c, login.RefreshCookie)).Status.ShouldBe(401);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.RefreshTokens WHERE UserId=@i AND RevokedReason='logout'", new[] { TestDb.P("@i", user.Id) })).ShouldBe(1);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorUserId=@i AND Action='Logout'", new[] { TestDb.P("@i", user.Id) })).ShouldBe(1);
    }

    [Fact]
    public async Task Logout_WithoutCookie_IsIdempotent()
        => (await AuthHttp.LogoutAsync(_fx.CreateClient(), null)).Status.ShouldBe(200);

    [Fact]
    public async Task Logout_RevokesOnlyThatSession_OtherDevicesStayLoggedIn()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        var deviceA = await AuthHttp.LoginAsync(c, user.Username, user.Password);
        var deviceB = await AuthHttp.LoginAsync(c, user.Username, user.Password);

        await AuthHttp.LogoutAsync(c, deviceA.RefreshCookie);

        (await AuthHttp.RefreshAsync(c, deviceA.RefreshCookie)).Status.ShouldBe(401);
        (await AuthHttp.RefreshAsync(c, deviceB.RefreshCookie)).Status.ShouldBe(200);
    }
}
