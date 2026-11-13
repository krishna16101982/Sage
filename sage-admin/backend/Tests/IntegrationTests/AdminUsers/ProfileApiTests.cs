using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.AdminUsers;

/// <summary>My Profile: any signed-in admin sees their own account and changes their own name and mobile number.</summary>
[Collection(ApiFixture.CollectionName)]
public class ProfileApiTests
{
    private readonly ApiFixture _fx;
    public ProfileApiTests(ApiFixture fx) { _fx = fx; }

    private Task<ApiResult> Call(HttpMethod m, string url, string? token, object? body = null)
        => AuthHttp.SendAsync(_fx.CreateClient(), m, url, body, token);

    [Fact]
    public async Task Profile_NeedsSignIn_ButNoPermission()
    {
        (await Call(HttpMethod.Get, "/api/admin/profile", null)).Status.ShouldBe(401);
        (await Call(HttpMethod.Put, "/api/admin/profile", null, new { firstName = "A", lastName = "B" })).Status.ShouldBe(401);

        var u = await TestData.CreateUserAsync(_fx.Db);   // no group, no permissions
        var token = (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;
        var me = await Call(HttpMethod.Get, "/api/admin/profile", token);
        me.Status.ShouldBe(200, me.Root.ToString());
        me.Data.GetProperty("id").GetGuid().ShouldBe(u.PublicId);
        me.Data.GetProperty("username").GetString().ShouldBe(u.Username);
        me.Data.GetProperty("email").GetString().ShouldBe(u.Email);
        me.Data.TryGetProperty("passwordUpdatedAtUtc", out _).ShouldBeTrue();
        me.Data.TryGetProperty("passwordHash", out _).ShouldBeFalse();
        // Signing in is in the audit log, so it shows as recent activity.
        me.Data.GetProperty("recentActivity").EnumerateArray().Select(a => a.GetProperty("action").GetString()).ShouldContain("Login");
    }

    [Fact]
    public async Task Profile_Update_ChangesOnlyNameAndMobile_AndIsAudited()
    {
        var u = await TestData.CreateUserAsync(_fx.Db);
        var token = (await AuthHttp.LoginAsync(_fx.CreateClient(), u.Username, u.Password)).AccessToken!;

        (await Call(HttpMethod.Put, "/api/admin/profile", token, new { firstName = "", lastName = "Kumar" })).Status.ShouldBe(400);
        (await Call(HttpMethod.Put, "/api/admin/profile", token, new { firstName = "Asha", lastName = "Kumar", mobileNumber = "not a phone" })).Status.ShouldBe(400);

        // Extra fields (email, status) are ignored: only an administrator changes those.
        var saved = await Call(HttpMethod.Put, "/api/admin/profile", token,
            new { firstName = " Asha ", lastName = "Kumar", mobileNumber = "+91 98765 43210", email = "other@example.com", status = "Inactive" });
        saved.Status.ShouldBe(200, saved.Root.ToString());
        saved.Data.GetProperty("firstName").GetString().ShouldBe("Asha");
        saved.Data.GetProperty("lastName").GetString().ShouldBe("Kumar");
        saved.Data.GetProperty("mobileNumber").GetString().ShouldBe("+91 98765 43210");
        saved.Data.GetProperty("email").GetString().ShouldBe(u.Email);
        saved.Data.GetProperty("status").GetString().ShouldBe("Active");
        saved.Data.GetProperty("recentActivity")[0].GetProperty("action").GetString().ShouldBe("UpdateProfile");

        // The header name comes from /auth/me, which now shows the new name; the session still works (no re-login needed).
        var me = await AuthHttp.MeAsync(_fx.CreateClient(), token);
        me.Status.ShouldBe(200);
        me.Data.GetProperty("firstName").GetString().ShouldBe("Asha");
    }
}
