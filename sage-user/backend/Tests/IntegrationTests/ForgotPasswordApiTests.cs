using SageUserApi.Tests.Infrastructure;
using Shouldly;

namespace SageUserApi.Tests.IntegrationTests;

/// <summary>User Panel "forgot password": emailed one-time link, same answer for unknown emails, sessions end on reset.</summary>
[Collection(UserApiFixture.CollectionName)]
public class ForgotPasswordApiTests
{
    private readonly UserApiFixture _fx;
    public ForgotPasswordApiTests(UserApiFixture fx) { _fx = fx; }

    private const string Auth = "/api/user/auth";
    private const string NewPassword = "N3w#Passw0rd!";
    private Task<ApiResult> Call(string path, object body, string? bearer = null, string? cookie = null)
        => UserHttp.SendAsync(_fx.CreateClient(), HttpMethod.Post, Auth + path, body, bearer, cookie);
    private static string Email(SalesUserRow u) => $"{u.Username}@sales.example.com";

    [Fact]
    public async Task ForgotPassword_SameAnswerForEveryone_AndOnlyActiveUsersGetALink()
    {
        var active = await SalesData.CreateAsync(_fx.Db);
        var inactive = await SalesData.CreateAsync(_fx.Db, status: "Inactive");

        var known = await Call("/forgot-password", new { email = Email(active) });
        var unknown = await Call("/forgot-password", new { email = "nobody-" + Guid.NewGuid().ToString("N")[..8] + "@example.com" });
        var off = await Call("/forgot-password", new { email = Email(inactive) });
        var notEmail = await Call("/forgot-password", new { email = "not an email" });

        foreach (var r in new[] { known, unknown, off, notEmail })
        {
            r.Status.ShouldBe(200);
            r.FirstMessage.ShouldBe("If the email belongs to an active account, a reset link has been sent.");
        }
        _fx.Notifier.LastTokenByEmail.ContainsKey(Email(active)).ShouldBeTrue();
        _fx.Notifier.LastTokenByEmail.ContainsKey(Email(inactive)).ShouldBeFalse();
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SalesUserPasswordResetTokens WHERE SalesUserId = @id", new[] { TestDb.P("@id", inactive.Id) })).ShouldBe(0);
    }

    [Fact]
    public async Task ResetPassword_WorksOnce_EndsSessions_AndTheNewPasswordSignsIn()
    {
        var u = await SalesData.CreateAsync(_fx.Db, mustChangePassword: true);
        var session = await UserHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password);
        session.Status.ShouldBe(200);

        (await Call("/forgot-password", new { email = Email(u) })).Status.ShouldBe(200);
        var token = _fx.Notifier.LastTokenByEmail[Email(u)];
        // Only the hash is stored, never the token itself.
        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SalesUserPasswordResetTokens WHERE TokenHash = @t", new[] { TestDb.P("@t", token) })).ShouldBe(0);

        (await Call("/reset-password", new { token, newPassword = "weak", confirmPassword = "weak" })).Status.ShouldBe(400);
        (await Call("/reset-password", new { token, newPassword = NewPassword, confirmPassword = NewPassword + "x" })).Status.ShouldBe(400);

        var done = await Call("/reset-password", new { token, newPassword = NewPassword, confirmPassword = NewPassword });
        done.Status.ShouldBe(200, done.Root.ToString());
        done.FirstMessage.ShouldBe("Password reset. Please sign in.");

        // The link works once.
        var again = await Call("/reset-password", new { token, newPassword = "An0ther#Passw0rd", confirmPassword = "An0ther#Passw0rd" });
        again.Status.ShouldBe(400);
        again.FirstMessage.ShouldBe("This reset link is invalid or has expired. Ask for a new one.");

        // The old session ends, the old password no longer works, the new one does (and need not be changed again).
        (await Call("/refresh-token", new { }, cookie: session.RefreshCookie)).Status.ShouldBe(401);
        (await UserHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).Status.ShouldBe(401);
        var fresh = await UserHttp.LoginAsync(_fx.CreateClient(), u.Username, NewPassword);
        fresh.Status.ShouldBe(200);
        fresh.Data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();

        (await _fx.Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityId = @e AND Action IN ('PasswordResetRequested', 'PasswordReset')",
            new[] { TestDb.P("@e", u.PublicId.ToString()) })).ShouldBe(2);
    }

    [Fact]
    public async Task ResetLinks_StopWorking_WhenANewOneIsSent_WhenExpired_OrForUnknownTokens()
    {
        var u = await SalesData.CreateAsync(_fx.Db);
        await Call("/forgot-password", new { email = Email(u) });
        var first = _fx.Notifier.LastTokenByEmail[Email(u)];
        await Call("/forgot-password", new { email = Email(u) });
        var second = _fx.Notifier.LastTokenByEmail[Email(u)];
        first.ShouldNotBe(second);

        (await Call("/reset-password", new { token = first, newPassword = NewPassword, confirmPassword = NewPassword })).Status.ShouldBe(400);
        (await Call("/reset-password", new { token = "made-up-token", newPassword = NewPassword, confirmPassword = NewPassword })).Status.ShouldBe(400);
        (await Call("/reset-password", new { token = "", newPassword = NewPassword, confirmPassword = NewPassword })).Status.ShouldBe(400);

        await _fx.Db.ExecuteNonQueryAsync("UPDATE dbo.SalesUserPasswordResetTokens SET ExpiresAtUtc = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE SalesUserId = @id AND UsedAtUtc IS NULL",
            new[] { TestDb.P("@id", u.Id) });
        (await Call("/reset-password", new { token = second, newPassword = NewPassword, confirmPassword = NewPassword })).Status.ShouldBe(400);
        (await UserHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).Status.ShouldBe(200);   // password unchanged
    }

    [Fact]
    public async Task ResetLink_StopsWorking_AfterThePasswordIsChangedAnotherWay()
    {
        var u = await SalesData.CreateAsync(_fx.Db);
        await Call("/forgot-password", new { email = Email(u) });
        var token = _fx.Notifier.LastTokenByEmail[Email(u)];

        var login = await UserHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password);
        (await Call("/change-password", new { currentPassword = u.Password, newPassword = NewPassword, confirmPassword = NewPassword }, login.AccessToken)).Status.ShouldBe(200);

        (await Call("/reset-password", new { token, newPassword = "An0ther#Passw0rd", confirmPassword = "An0ther#Passw0rd" })).Status.ShouldBe(400);
        (await UserHttp.LoginAsync(_fx.CreateClient(), u.Username, NewPassword)).Status.ShouldBe(200);
    }
}
