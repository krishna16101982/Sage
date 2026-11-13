using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Security;
using SageUserApi.AppFunctions.Interfaces;

namespace SageUserApi.Tests.Infrastructure;

/// <summary>Captures "forgot password" tokens instead of sending email.</summary>
public sealed class CapturingNotifier : IPasswordResetNotifier
{
    public ConcurrentDictionary<string, string> LastTokenByEmail { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Task SendAsync(string email, string firstName, string rawToken, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        LastTokenByEmail[email] = rawToken;
        return Task.CompletedTask;
    }
}

/// <summary>A throw-away database plus an in-memory User API wired to it (fast password hashing, no rate-limit noise).</summary>
public sealed class UserApiFixture : IAsyncLifetime
{
    public const string CollectionName = "UserApi";
    public const string JwtKey = "user-api-test-signing-key-0123456789-xyz";
    public const string Issuer = "sage.user";
    public const string Audience = "sage.user.app";

    private readonly SqlServerFixture _sql = new();
    public WebApplicationFactory<Program> Factory { get; private set; } = default!;
    public IDatabaseFunctions Db { get; private set; } = default!;
    public CapturingNotifier Notifier { get; } = new();

    public async Task InitializeAsync()
    {
        await _sql.InitializeAsync();
        Db = TestDb.Create(_sql.ConnectionString);
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:ConnectionString"] = _sql.ConnectionString,
                ["DatabaseSettings:MaxRetryCount"] = "0",
                ["JwtIssuerOptions:SecurityKey"] = JwtKey,
                ["AuthSettings:PasswordHashIterations"] = "1000",
                ["AuthSettings:RefreshCookieSecure"] = "false",
                ["RateLimiting:AuthPermitPerMinute"] = "100000",
                ["Serilog:FilePath"] = Path.Combine(Path.GetTempPath(), "sage-user-tests-logs"),
            }));
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordResetNotifier>();
                services.AddSingleton<IPasswordResetNotifier>(Notifier);
            });
        });
    }

    /// <summary>Cookies are handled manually by the tests so they can replay old refresh tokens.</summary>
    public HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

[CollectionDefinition(UserApiFixture.CollectionName)]
public class UserApiCollection : ICollectionFixture<UserApiFixture> { }

public record SalesUserRow(long Id, Guid PublicId, string Username, string Password);

public static class SalesData
{
    public const string Password = "Str0ng#Passw0rd";
    private static int _counter;

    /// <summary>Insert a sales manager, or a sales person under `manager` (creates one when not given).</summary>
    public static async Task<SalesUserRow> CreateAsync(IDatabaseFunctions db, string role = "SalesManager", SalesUserRow? manager = null,
        string status = "Active", bool mustChangePassword = false)
    {
        if (role == "SalesPerson" && manager == null) manager = await CreateAsync(db);
        var n = Interlocked.Increment(ref _counter);
        var username = $"sales{n}_{Guid.NewGuid():N}"[..20];
        var id = await db.ExecuteScalarAsync<long>(
            @"INSERT dbo.SalesUsers (FirstName, LastName, Username, Email, PasswordHash, MustChangePassword, Role, ManagerId, Status)
              OUTPUT INSERTED.Id VALUES (N'Sam', N'Seller', @u, @e, @h, @m, @r, @mgr, @s)",
            new[]
            {
                TestDb.P("@u", username), TestDb.P("@e", $"{username}@sales.example.com"), TestDb.P("@h", SecurityHash.HashPassword(Password, 1000)),
                TestDb.P("@m", mustChangePassword), TestDb.P("@r", role), TestDb.P("@mgr", manager?.Id), TestDb.P("@s", status)
            });
        var publicId = await db.ExecuteScalarAsync<Guid>("SELECT PublicId FROM dbo.SalesUsers WHERE Id = @id", new[] { TestDb.P("@id", id) });
        return new SalesUserRow(id, publicId, username, Password);
    }
}

public record ApiResult(HttpResponseMessage Response, JsonElement Root)
{
    public int Status => (int)Response.StatusCode;
    public JsonElement Data => Root.GetProperty("data");
    public string FirstMessage => Root.GetProperty("message")[0].GetString()!;
    public string? AccessToken => Root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("accessToken", out var t) ? t.GetString() : null;

    /// <summary>Value of the sage_user_rt cookie set by this response (null if absent or deleted).</summary>
    public string? RefreshCookie
    {
        get
        {
            if (!Response.Headers.TryGetValues("Set-Cookie", out var values)) return null;
            var raw = values.FirstOrDefault(v => v.StartsWith("sage_user_rt=", StringComparison.Ordinal))?.Split(';')[0]["sage_user_rt=".Length..];
            return string.IsNullOrEmpty(raw) ? null : raw;
        }
    }
}

public static class UserHttp
{
    public static async Task<ApiResult> SendAsync(HttpClient c, HttpMethod m, string url, object? body = null, string? bearer = null, string? refreshCookie = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (body != null) req.Content = JsonContent.Create(body);
        if (bearer != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (refreshCookie != null) req.Headers.Add("Cookie", $"sage_user_rt={refreshCookie}");
        var res = await c.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
        return new ApiResult(res, doc.RootElement.Clone());
    }

    public static Task<ApiResult> LoginAsync(HttpClient c, string username, string password)
        => SendAsync(c, HttpMethod.Post, "/api/user/auth/login", new { username, password });

    /// <summary>Forge a JWT (used to prove that tokens from another issuer/key are refused).</summary>
    public static string ForgeToken(Guid sub, int pv, string key, string issuer, string audience)
    {
        var jwt = new JwtSecurityToken(issuer, audience, new[] { new Claim(JwtRegisteredClaimNames.Sub, sub.ToString()), new Claim("pv", pv.ToString()) },
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
