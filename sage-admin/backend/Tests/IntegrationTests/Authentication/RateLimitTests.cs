using SageAdminApi.Tests.Infrastructure;
using Shouldly;

namespace SageAdminApi.Tests.IntegrationTests.Authentication;

[Collection(ApiFixture.CollectionName)]
public class RateLimitTests
{
    private readonly ApiFixture _fx;
    public RateLimitTests(ApiFixture fx) { _fx = fx; }

    [Fact]
    public async Task AuthEndpoints_AreRateLimitedPerIp_With429Envelope()
    {
        await using var factory = _fx.CreateFactory(new() { ["RateLimiting:AuthPermitPerMinute"] = "3" });
        var client = factory.CreateClient();

        var statuses = new List<int>();
        ApiResult? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = await AuthHttp.LoginAsync(client, "nobody", "Whatever#12345");
            statuses.Add(last.Status);
        }

        statuses.Take(3).ShouldAllBe(s => s == 401);
        statuses.Skip(3).ShouldAllBe(s => s == 429);
        last!.Root.GetProperty("status").GetBoolean().ShouldBeFalse();
    }
}
