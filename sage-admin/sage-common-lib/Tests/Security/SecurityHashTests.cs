using SageCommonLib.Security;
using Shouldly;

namespace SageCommonLib.Tests.Security;

public class SecurityHashTests
{
    [Fact]
    public void HashThenVerify_CorrectPassword_ReturnsTrue()
    {
        var hash = SecurityHash.HashPassword("Correct#Horse9", _iterations: 1000);
        SecurityHash.VerifyHashedPassword(hash, "Correct#Horse9").ShouldBeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
        => SecurityHash.VerifyHashedPassword(SecurityHash.HashPassword("Correct#Horse9", 1000), "wrong").ShouldBeFalse();

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentSalts()
        => SecurityHash.HashPassword("Same#Pass123", 1000).ShouldNotBe(SecurityHash.HashPassword("Same#Pass123", 1000));

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("v1.x.y.z")]
    [InlineData("v2.1000.AAAA.AAAA")]
    public void Verify_MalformedHash_ReturnsFalse(string hash)
        => SecurityHash.VerifyHashedPassword(hash, "anything").ShouldBeFalse();

    [Fact]
    public void NeedsRehash_LowIterations_ReturnsTrue()
    {
        SecurityHash.NeedsRehash(SecurityHash.HashPassword("Abcdef#1234", 1000)).ShouldBeTrue();
        SecurityHash.NeedsRehash(SecurityHash.HashPassword("Abcdef#1234", 1000), 500).ShouldBeFalse();
    }

    [Fact]
    public void TokenGenerator_ProducesUniqueUrlSafeTokens_AndStableHash()
    {
        var a = TokenGenerator.CreateSecureToken();
        var b = TokenGenerator.CreateSecureToken();
        a.ShouldNotBe(b);
        a.ShouldNotContain("+"); a.ShouldNotContain("/"); a.ShouldNotContain("=");
        TokenGenerator.Hash(a).ShouldBe(TokenGenerator.Hash(a));
        TokenGenerator.Hash(a).Length.ShouldBe(64);
    }
}
