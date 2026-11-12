using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using SageCommonLib.Security;

namespace SageAdminApi.Tests.Infrastructure;

public record TestUser(long Id, Guid PublicId, string Username, string Email, string Password);

public record ApiResult(HttpResponseMessage Response, JsonElement Root)
{
    public int Status => (int)Response.StatusCode;
    public JsonElement Data => Root.GetProperty("data");
    public string FirstMessage => Root.GetProperty("message")[0].GetString()!;
    public string? AccessToken => Root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("accessToken", out var t) ? t.GetString() : null;

    /// <summary>Value of the sage_rt cookie set by this response (null if absent or deleted).</summary>
    public string? RefreshCookie
    {
        get
        {
            if (!Response.Headers.TryGetValues("Set-Cookie", out var values)) return null;
            var cookie = values.FirstOrDefault(v => v.StartsWith("sage_rt=", StringComparison.Ordinal));
            var raw = cookie?.Split(';')[0]["sage_rt=".Length..];
            return string.IsNullOrEmpty(raw) ? null : raw;
        }
    }

    public string? SetCookieHeader => Response.Headers.TryGetValues("Set-Cookie", out var v) ? v.FirstOrDefault(x => x.StartsWith("sage_rt=", StringComparison.Ordinal)) : null;
}

public static class TestData
{
    public const string StrongPassword = "Str0ng#Passw0rd";
    private static int _counter;

    public static async Task<TestUser> CreateUserAsync(SageCommonLib.AppFunctions.Interfaces.IDatabaseFunctions db, string? password = null, string status = "Active",
        bool isSuperAdmin = false, bool mustChangePassword = false)
    {
        password ??= StrongPassword;
        var n = Interlocked.Increment(ref _counter);
        var username = $"user{n}_{Guid.NewGuid():N}"[..20];
        var email = $"{username}@test.example.com";
        var id = await db.ExecuteScalarAsync<long>(
            @"INSERT dbo.AdminUsers (FirstName, LastName, Username, Email, PasswordHash, MustChangePassword, IsSuperAdmin, Status)
              OUTPUT INSERTED.Id VALUES (N'Test', N'User', @u, @e, @h, @m, @s, @st)",
            new[]
            {
                TestDb.P("@u", username), TestDb.P("@e", email), TestDb.P("@h", SecurityHash.HashPassword(password, 1000)),
                TestDb.P("@m", mustChangePassword), TestDb.P("@s", isSuperAdmin), TestDb.P("@st", status)
            });
        var publicId = await db.ExecuteScalarAsync<Guid>("SELECT PublicId FROM dbo.AdminUsers WHERE Id = @id", new[] { TestDb.P("@id", id) });
        return new TestUser(id, publicId, username, email, password);
    }

    /// <summary>Create a group holding the given permission codes and make it the user's primary group.</summary>
    public static async Task AssignGroupAsync(SageCommonLib.AppFunctions.Interfaces.IDatabaseFunctions db, TestUser user, params string[] permissionCodes)
    {
        var code = "G" + Guid.NewGuid().ToString("N")[..10];
        await db.ExecuteNonQueryAsync("INSERT dbo.PermissionGroups (Name, Code) VALUES (@c, @c)", new[] { TestDb.P("@c", code) });
        foreach (var p in permissionCodes)
            await db.ExecuteNonQueryAsync(
                "INSERT dbo.GroupPermissions (GroupId, PermissionId) SELECT g.Id, p.Id FROM dbo.PermissionGroups g, dbo.Permissions p WHERE g.Code = @g AND p.Code = @p",
                new[] { TestDb.P("@g", code), TestDb.P("@p", p) });
        await db.ExecuteNonQueryAsync(
            "INSERT dbo.AdminUserGroups (UserId, GroupId, IsPrimary) SELECT @u, Id, 1 FROM dbo.PermissionGroups WHERE Code = @g",
            new[] { TestDb.P("@u", user.Id), TestDb.P("@g", code) });
    }
}

public static class AuthHttp
{
    private static async Task<ApiResult> ReadAsync(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
        return new ApiResult(res, doc.RootElement.Clone());
    }

    private static HttpRequestMessage Req(HttpMethod method, string url, object? body = null, string? bearer = null, string? refreshCookie = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body != null) req.Content = JsonContent.Create(body);
        if (bearer != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (refreshCookie != null) req.Headers.Add("Cookie", $"sage_rt={refreshCookie}");
        return req;
    }

    public static async Task<ApiResult> SendAsync(HttpClient c, HttpMethod m, string url, object? body = null, string? bearer = null, string? refreshCookie = null)
        => await ReadAsync(await c.SendAsync(Req(m, url, body, bearer, refreshCookie)));

    public static Task<ApiResult> LoginAsync(HttpClient c, string username, string password)
        => SendAsync(c, HttpMethod.Post, "/api/admin/auth/login", new { username, password });

    public static Task<ApiResult> RefreshAsync(HttpClient c, string? cookie)
        => SendAsync(c, HttpMethod.Post, "/api/admin/auth/refresh-token", null, null, cookie);

    public static Task<ApiResult> LogoutAsync(HttpClient c, string? cookie)
        => SendAsync(c, HttpMethod.Post, "/api/admin/auth/logout", null, null, cookie);

    public static Task<ApiResult> MeAsync(HttpClient c, string? bearer)
        => SendAsync(c, HttpMethod.Get, "/api/admin/auth/me", null, bearer);

    /// <summary>Forge a JWT signed with the test key (used for expired / tampered token tests).</summary>
    public static string ForgeToken(Guid sub, int permissionVersion, DateTime notBefore, DateTime expires, string key = ApiFixture.JwtKey)
    {
        var jwt = new JwtSecurityToken(ApiFixture.Issuer, ApiFixture.Audience,
            new[] { new Claim(JwtRegisteredClaimNames.Sub, sub.ToString()), new Claim("pv", permissionVersion.ToString()) },
            notBefore, expires, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
