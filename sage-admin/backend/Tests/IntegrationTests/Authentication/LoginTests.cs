using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Authentication;

[Collection(ApiFixture.CollectionName)]
public class LoginTests
{
    private readonly ApiFixture _fx;
    public LoginTests(ApiFixture fx) { _fx = fx; }

    [Fact]
    public async Task ValidLogin_ReturnsAccessToken_SetsHttpOnlyRefreshCookie_AndKeepsRefreshTokenOutOfBody()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        var res = await AuthHttp.LoginAsync(_fx.CreateClient(), user.Username, user.Password);

        res.Status.ShouldBe(200);
        res.AccessToken.ShouldNotBeNullOrEmpty();
        res.Data.GetProperty("expiresIn").GetInt32().ShouldBeGreaterThan(0);
        res.Data.GetProperty("user").GetProperty("username").GetString().ShouldBe(user.Username);
        res.Data.GetProperty("user").GetProperty("id").GetGuid().ShouldBe(user.PublicId);
        res.RefreshCookie.ShouldNotBeNullOrEmpty();
        res.SetCookieHeader!.ToLowerInvariant().ShouldContain("httponly");
        res.SetCookieHeader.ToLowerInvariant().ShouldContain("path=/api/admin/auth");
        res.Root.GetRawText().ShouldNotContain(res.RefreshCookie!);
        res.Root.GetRawText().ShouldNotContain("passwordHash", Case.Insensitive);
    }

    [Fact]
    public async Task ValidLogin_ByEmail_Works()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        (await AuthHttp.LoginAsync(_fx.CreateClient(), user.Email, user.Password)).Status.ShouldBe(200);
    }

    [Fact]
    public async Task ValidLogin_ReportsMustChangePassword()
    {
        var user = await TestData.CreateUserAsync(_fx.Db, mustChangePassword: true);
        var res = await AuthHttp.LoginAsync(_fx.CreateClient(), user.Username, user.Password);
        res.Data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidLogin_UnknownUser_Returns401WithGenericMessage()
    {
        var res = await AuthHttp.LoginAsync(_fx.CreateClient(), "nobody_" + Guid.NewGuid().ToString("N")[..8], "Whatever#12345");
        res.Status.ShouldBe(401);
        res.FirstMessage.ShouldBe("Invalid username or password.");
        res.RefreshCookie.ShouldBeNull();
    }

    [Fact]
    public async Task IncorrectPassword_Returns401_SameMessageAsUnknownUser_AndCountsFailure()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        var res = await AuthHttp.LoginAsync(_fx.CreateClient(), user.Username, "Wrong#Password1");
        res.Status.ShouldBe(401);
        res.FirstMessage.ShouldBe("Invalid username or password.");
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT FailedLoginCount FROM dbo.AdminUsers WHERE Id=@i", new[] { TestDb.P("@i", user.Id) })).ShouldBe(1);
    }

    [Theory]
    [InlineData("Inactive")]
    [InlineData("Locked")]
    public async Task NonActiveUser_CannotLogin_EvenWithCorrectPassword(string status)
    {
        var user = await TestData.CreateUserAsync(_fx.Db, status: status);
        var res = await AuthHttp.LoginAsync(_fx.CreateClient(), user.Username, user.Password);
        res.Status.ShouldBe(401);
        res.FirstMessage.ShouldBe("Invalid username or password.");
    }

    [Fact]
    public async Task SoftDeletedUser_CannotLogin()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.AdminUsers SET IsDeleted = 1 WHERE Id=@i", new[] { TestDb.P("@i", user.Id) });
        (await AuthHttp.LoginAsync(_fx.CreateClient(), user.Username, user.Password)).Status.ShouldBe(401);
    }

    [Fact]
    public async Task MissingFields_Returns400()
    {
        var res = await AuthHttp.LoginAsync(_fx.CreateClient(), "", "");
        res.Status.ShouldBe(400);
    }

    [Fact]
    public async Task LoginAttemptProtection_LocksAfterFiveFailures_EvenForTheCorrectPassword()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        var client = _fx.CreateClient();
        for (var i = 0; i < 5; i++)
            (await AuthHttp.LoginAsync(client, user.Username, "Wrong#Password1")).Status.ShouldBe(401);

        var locked = await AuthHttp.LoginAsync(client, user.Username, user.Password);
        locked.Status.ShouldBe(429);
        locked.RefreshCookie.ShouldBeNull();
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AdminUsers WHERE Id=@i AND LockoutEndUtc > SYSUTCDATETIME()", new[] { TestDb.P("@i", user.Id) })).ShouldBe(1);
    }

    [Fact]
    public async Task LoginAttemptProtection_ExpiredLockout_AllowsLoginAndResetsCounters()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.AdminUsers SET LockoutEndUtc = DATEADD(MINUTE,-1,SYSUTCDATETIME()), FailedLoginCount = 3 WHERE Id=@i", new[] { TestDb.P("@i", user.Id) });
        (await AuthHttp.LoginAsync(_fx.CreateClient(), user.Username, user.Password)).Status.ShouldBe(200);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT FailedLoginCount FROM dbo.AdminUsers WHERE Id=@i AND LockoutEndUtc IS NULL", new[] { TestDb.P("@i", user.Id) })).ShouldBe(0);
    }

    [Fact]
    public async Task LoginAttemptProtection_SuccessResetsFailureCounter()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        var client = _fx.CreateClient();
        for (var i = 0; i < 3; i++) await AuthHttp.LoginAsync(client, user.Username, "Wrong#Password1");
        (await AuthHttp.LoginAsync(client, user.Username, user.Password)).Status.ShouldBe(200);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT FailedLoginCount FROM dbo.AdminUsers WHERE Id=@i", new[] { TestDb.P("@i", user.Id) })).ShouldBe(0);
    }

    [Fact]
    public async Task LoginHistory_RecordsSuccessAndFailureWithoutPasswords()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        var client = _fx.CreateClient();
        await AuthHttp.LoginAsync(client, user.Username, "Wrong#Password1");
        await AuthHttp.LoginAsync(client, user.Username, user.Password);

        var rows = await _fx.Db.QueryAsync("SELECT IsSuccess, FailureReason FROM dbo.LoginHistories WHERE UserId=@i ORDER BY Id",
            r => (Ok: r.GetBoolean(0), Reason: r.IsDBNull(1) ? null : r.GetString(1)), new[] { TestDb.P("@i", user.Id) });
        rows.Count.ShouldBe(2);
        rows[0].ShouldBe((false, "BadPassword"));
        rows[1].Ok.ShouldBeTrue();
    }

    [Fact]
    public async Task LoginHistory_RecordsUnknownUsernames()
    {
        var name = "ghost_" + Guid.NewGuid().ToString("N")[..8];
        await AuthHttp.LoginAsync(_fx.CreateClient(), name, "Whatever#12345");
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.LoginHistories WHERE UsernameAttempted=@n AND UserId IS NULL AND FailureReason='UnknownUser'", new[] { TestDb.P("@n", name) })).ShouldBe(1);
    }

    [Fact]
    public async Task Login_StoresRefreshTokenHashed_NeverTheRawToken()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        var res = await AuthHttp.LoginAsync(_fx.CreateClient(), user.Username, user.Password);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.RefreshTokens WHERE TokenHash=@t", new[] { TestDb.P("@t", res.RefreshCookie) })).ShouldBe(0);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.RefreshTokens WHERE UserId=@i", new[] { TestDb.P("@i", user.Id) })).ShouldBe(1);
    }

    [Fact]
    public async Task Login_WritesAuditLog()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        await AuthHttp.LoginAsync(_fx.CreateClient(), user.Username, user.Password);
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorUserId=@i AND Action='Login'", new[] { TestDb.P("@i", user.Id) })).ShouldBe(1);
    }
}
