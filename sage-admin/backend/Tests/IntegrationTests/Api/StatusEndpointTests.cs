using Microsoft.AspNetCore.Hosting;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Api;

[Collection(SqlServerFixture.CollectionName)]
public class StatusEndpointTests
{
    private readonly SqlServerFixture _fx;
    public StatusEndpointTests(SqlServerFixture fx) { _fx = fx; }

    private WebApplicationFactory<Program> Factory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:ConnectionString"] = connectionString,
                ["DatabaseSettings:MaxRetryCount"] = "0",
                ["JwtIssuerOptions:SecurityKey"] = "test-only-signing-key-0123456789-abcdef",
                ["Serilog:FilePath"] = Path.Combine(Path.GetTempPath(), "sage-tests-logs"),
            }));
        });

    [Fact]
    public async Task Status_AnonymousAllowed_ReturnsOkWhenDatabaseUp()
    {
        await using var f = Factory(_fx.ConnectionString);
        var res = await f.CreateClient().GetAsync("/api/admin/status");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.ShouldContain("\"database\":\"ok\"");
        res.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
    }

    [Fact]
    public async Task Status_Returns503WhenDatabaseDown_WithoutLeakingConnectionDetails()
    {
        await using var f = Factory("Server=127.0.0.1,1;Database=x;User Id=nobody;Password=secret-pw;Connect Timeout=2;Encrypt=false");
        var res = await f.CreateClient().GetAsync("/api/admin/status");
        res.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await res.Content.ReadAsStringAsync()).ShouldNotContain("secret-pw");
    }

    [Fact]
    public async Task UnknownRoute_RequiresAuthentication_Returns401()
    {
        await using var f = Factory(_fx.ConnectionString);
        (await f.CreateClient().GetAsync("/api/admin/nope")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
