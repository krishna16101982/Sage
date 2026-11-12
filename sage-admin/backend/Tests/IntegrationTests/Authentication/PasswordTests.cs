using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Authentication;

[Collection(ApiFixture.CollectionName)]
public class PasswordTests
{
    private const string NewPassword = "N3w#Passw0rdXyz";
    private readonly ApiFixture _fx;
    public PasswordTests(ApiFixture fx) { _fx = fx; }

    private Task<ApiResult> Forgot(HttpClient c, string email) => AuthHttp.SendAsync(c, HttpMethod.Post, "/api/admin/auth/forgot-password", new { email });
    private Task<ApiResult> Reset(HttpClient c, string? token, string pw, string? confirm = null)
        => AuthHttp.SendAsync(c, HttpMethod.Post, "/api/admin/auth/reset-password", new { token, newPassword = pw, confirmPassword = confirm ?? pw });
    private Task<ApiResult> Change(HttpClient c, string bearer, string current, string pw, string? confirm = null)
        => AuthHttp.SendAsync(c, HttpMethod.Post, "/api/admin/auth/change-password", new { currentPassword = current, newPassword = pw, confirmPassword = confirm ?? pw }, bearer);

    // ---------- forgot / reset ----------

    [Fact]
    public async Task ForgotPassword_UnknownEmail_AnswersTheSame_AndSendsNothing()
    {
        var before = _fx.Notifier.SentCount;
        var unknown = await Forgot(_fx.CreateClient(), $"nobody_{Guid.NewGuid():N}@test.example.com");
        var user = await TestData.CreateUserAsync(_fx.Db);
        var known = await Forgot(_fx.CreateClient(), user.Email);

        unknown.Status.ShouldBe(200);
        known.Status.ShouldBe(200);
        known.FirstMessage.ShouldBe(unknown.FirstMessage);
        _fx.Notifier.SentCount.ShouldBe(before + 1);   // only the real account got a token
    }

    [Fact]
    public async Task ForgotPassword_InactiveUser_SendsNothing()
    {
        var user = await TestData.CreateUserAsync(_fx.Db, status: "Inactive");
        var before = _fx.Notifier.SentCount;
        (await Forgot(_fx.CreateClient(), user.Email)).Status.ShouldBe(200);
        _fx.Notifier.SentCount.ShouldBe(before);
    }

    [Fact]
    public async Task ForgotPassword_InvalidEmailFormat_StillGenericOk()
        => (await Forgot(_fx.CreateClient(), "not-an-email")).Status.ShouldBe(200);

    [Fact]
    public async Task ResetPassword_FullFlow_ChangesPassword_RevokesSessions_AndTokenIsSingleUse()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db, status: "Active", mustChangePassword: true);
        var login = await AuthHttp.LoginAsync(c, user.Username, user.Password);
        await Forgot(c, user.Email);
        var token = _fx.Notifier.LastTokenByEmail[user.Email];

        var reset = await Reset(c, token, NewPassword);

        reset.Status.ShouldBe(200);
        (await AuthHttp.LoginAsync(c, user.Username, user.Password)).Status.ShouldBe(401);       // old password dead
        (await AuthHttp.LoginAsync(c, user.Username, NewPassword)).Status.ShouldBe(200);          // new works
        (await AuthHttp.RefreshAsync(c, login.RefreshCookie)).Status.ShouldBe(401);               // old session revoked
        (await AuthHttp.MeAsync(c, login.AccessToken)).Status.ShouldBe(401);                      // old access token dead
        (await Reset(c, token, "An0ther#Passw0rd1")).Status.ShouldBe(400);                        // single use
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PasswordResetTokens WHERE UserId=@i AND TokenHash=@t", new[] { TestDb.P("@i", user.Id), TestDb.P("@t", token) })).ShouldBe(0); // stored hashed
    }

    [Fact]
    public async Task ResetPassword_ClearsLockout()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        for (var i = 0; i < 5; i++) await AuthHttp.LoginAsync(c, user.Username, "Wrong#Password1");
        (await AuthHttp.LoginAsync(c, user.Username, user.Password)).Status.ShouldBe(429);

        await Forgot(c, user.Email);
        (await Reset(c, _fx.Notifier.LastTokenByEmail[user.Email], NewPassword)).Status.ShouldBe(200);
        (await AuthHttp.LoginAsync(c, user.Username, NewPassword)).Status.ShouldBe(200);
    }

    [Fact]
    public async Task ResetPassword_NewForgotRequest_InvalidatesThePreviousToken()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        await Forgot(c, user.Email);
        var first = _fx.Notifier.LastTokenByEmail[user.Email];
        await Forgot(c, user.Email);
        var second = _fx.Notifier.LastTokenByEmail[user.Email];

        (await Reset(c, first, NewPassword)).Status.ShouldBe(400);
        (await Reset(c, second, NewPassword)).Status.ShouldBe(200);
    }

    [Fact]
    public async Task ResetPassword_InvalidOrMissingToken_Returns400()
    {
        var c = _fx.CreateClient();
        (await Reset(c, "bogus-token", NewPassword)).Status.ShouldBe(400);
        (await Reset(c, null, NewPassword)).Status.ShouldBe(400);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_Returns400()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        await Forgot(c, user.Email);
        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.PasswordResetTokens SET ExpiresAtUtc = DATEADD(MINUTE,-1,SYSUTCDATETIME()) WHERE UserId=@i", new[] { TestDb.P("@i", user.Id) });
        (await Reset(c, _fx.Notifier.LastTokenByEmail[user.Email], NewPassword)).Status.ShouldBe(400);
    }

    [Theory]
    [InlineData("short1A!", null)]
    [InlineData("alllowercase#12345", null)]
    [InlineData(NewPassword, "Different#Passw0rd9")]
    public async Task ResetPassword_WeakOrMismatchedPassword_Returns400_AndKeepsTokenUsable(string pw, string? confirm)
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        await Forgot(c, user.Email);
        var token = _fx.Notifier.LastTokenByEmail[user.Email];

        (await Reset(c, token, pw, confirm)).Status.ShouldBe(400);
        (await Reset(c, token, NewPassword)).Status.ShouldBe(200);
    }

    // ---------- change password ----------

    [Fact]
    public async Task ChangePassword_Success_RevokesAllSessions_AndOldAccessToken()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db, mustChangePassword: true);
        var login = await AuthHttp.LoginAsync(c, user.Username, user.Password);

        var res = await Change(c, login.AccessToken!, user.Password, NewPassword);

        res.Status.ShouldBe(200);
        (await AuthHttp.MeAsync(c, login.AccessToken)).Status.ShouldBe(401);
        (await AuthHttp.RefreshAsync(c, login.RefreshCookie)).Status.ShouldBe(401);
        (await AuthHttp.LoginAsync(c, user.Username, user.Password)).Status.ShouldBe(401);
        var relogin = await AuthHttp.LoginAsync(c, user.Username, NewPassword);
        relogin.Status.ShouldBe(200);
        relogin.Data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorUserId=@i AND Action='PasswordChange'", new[] { TestDb.P("@i", user.Id) })).ShouldBe(1);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns400_AndChangesNothing()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        var login = await AuthHttp.LoginAsync(c, user.Username, user.Password);

        (await Change(c, login.AccessToken!, "Wrong#Password1", NewPassword)).Status.ShouldBe(400);
        (await AuthHttp.LoginAsync(c, user.Username, user.Password)).Status.ShouldBe(200);
    }

    [Fact]
    public async Task ChangePassword_SameAsCurrent_Returns400()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        var login = await AuthHttp.LoginAsync(c, user.Username, user.Password);
        (await Change(c, login.AccessToken!, user.Password, user.Password)).Status.ShouldBe(400);
    }

    [Fact]
    public async Task ChangePassword_WeakOrMismatched_Returns400()
    {
        var c = _fx.CreateClient();
        var user = await TestData.CreateUserAsync(_fx.Db);
        var login = await AuthHttp.LoginAsync(c, user.Username, user.Password);
        (await Change(c, login.AccessToken!, user.Password, "weak")).Status.ShouldBe(400);
        (await Change(c, login.AccessToken!, user.Password, NewPassword, "Mismatch#Passw0rd1")).Status.ShouldBe(400);
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        var res = await AuthHttp.SendAsync(_fx.CreateClient(), HttpMethod.Post, "/api/admin/auth/change-password",
            new { currentPassword = "x", newPassword = NewPassword, confirmPassword = NewPassword });
        res.Status.ShouldBe(401);
    }

    [Fact]
    public async Task Passwords_AreStoredHashed_NeverPlaintext()
    {
        var user = await TestData.CreateUserAsync(_fx.Db);
        var hash = await _fx.Db.ExecuteScalarAsync<string>("SELECT PasswordHash FROM dbo.AdminUsers WHERE Id=@i", new[] { TestDb.P("@i", user.Id) });
        hash.ShouldStartWith("v1.");
        hash.ShouldNotContain(user.Password);
    }
}
