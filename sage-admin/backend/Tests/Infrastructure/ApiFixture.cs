using Microsoft.AspNetCore.TestHost;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SageAdminApi.AppFunctions.Interfaces;
using SageCommonLib.AppFunctions.Interfaces;

namespace SageAdminApi.Tests.Infrastructure;

/// <summary>Captures password-reset tokens instead of sending email.</summary>
public sealed class CapturingNotifier : IPasswordResetNotifier
{
    public ConcurrentDictionary<string, string> LastTokenByEmail { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int SentCount;

    public Task SendAsync(string email, string firstName, string rawToken, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        LastTokenByEmail[email] = rawToken;
        Interlocked.Increment(ref SentCount);
        return Task.CompletedTask;
    }
}

/// <summary>A throw-away database plus an in-memory API host wired to it (fast password hashing, no rate-limit noise).</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string CollectionName = "Api";
    public const string JwtKey = "test-only-signing-key-0123456789-abcdef";
    public const string Issuer = "sage.admin";
    public const string Audience = "sage.admin.app";

    private readonly SqlServerFixture _sql = new();
    public CapturingNotifier Notifier { get; } = new();
    public WebApplicationFactory<Program> Factory { get; private set; } = default!;
    public IDatabaseFunctions Db { get; private set; } = default!;
    public string ConnectionString => _sql.ConnectionString;

    public async Task InitializeAsync()
    {
        await _sql.InitializeAsync();
        Db = TestDb.Create(_sql.ConnectionString);
        Factory = CreateFactory();
    }

    public WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?>? overrides = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["DatabaseSettings:ConnectionString"] = _sql.ConnectionString,
                    ["DatabaseSettings:MaxRetryCount"] = "0",
                    ["JwtIssuerOptions:SecurityKey"] = JwtKey,
                    ["AuthSettings:PasswordHashIterations"] = "1000",
                    ["AuthSettings:RefreshCookieSecure"] = "false",
                    ["RateLimiting:AuthPermitPerMinute"] = "100000",
                    ["Serilog:FilePath"] = Path.Combine(Path.GetTempPath(), "sage-tests-logs"),
                };
                if (overrides != null) foreach (var kv in overrides) values[kv.Key] = kv.Value;
                cfg.AddInMemoryCollection(values);
            });
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IPasswordResetNotifier>();
                s.AddSingleton<IPasswordResetNotifier>(Notifier);
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

[CollectionDefinition(ApiFixture.CollectionName)]
public class ApiCollection : ICollectionFixture<ApiFixture> { }
