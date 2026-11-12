using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SageAdminApi.AppFunctions;
using SageAdminApi.AppValidations;
using SageAdminApi.Models;
using SageCommonLib.Models;
using Shouldly;

namespace SageAdminApi.Tests.UnitTests.Authentication;

public class TokenAndValidationUnitTests
{
    private static readonly JwtIssuerOptions Jwt = new()
    {
        Issuer = "sage.admin", Audience = "sage.admin.app", SecurityKey = "unit-test-signing-key-0123456789-abcdefgh", AccessTokenMinutes = 15
    };

    private static AdminUser User(bool superAdmin = false) => new()
    {
        Id = 7, PublicId = Guid.NewGuid(), Username = "rahul", IsSuperAdmin = superAdmin, PermissionVersion = 4
    };

    [Fact]
    public void AccessToken_CarriesIdentityAndPermissionVersion_ButNoPermissionList()
    {
        var user = User(superAdmin: true);
        var (token, expiresIn) = new TokenFunctions(Options.Create(Jwt)).CreateAccessToken(user);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        parsed.Subject.ShouldBe(user.PublicId.ToString());
        parsed.Claims.First(c => c.Type == "pv").Value.ShouldBe("4");
        parsed.Claims.First(c => c.Type == "sa").Value.ShouldBe("1");
        parsed.Claims.ShouldNotContain(c => c.Type == "permissions" || c.Type == "role");
        parsed.Issuer.ShouldBe("sage.admin");
        parsed.Audiences.ShouldContain("sage.admin.app");
        expiresIn.ShouldBe(15 * 60);
        (parsed.ValidTo - DateTime.UtcNow).TotalMinutes.ShouldBeInRange(14, 15.1);
    }

    [Fact]
    public void AccessToken_ValidatesWithTheConfiguredKey_AndFailsWithAnother()
    {
        var (token, _) = new TokenFunctions(Options.Create(Jwt)).CreateAccessToken(User());
        TokenValidationParameters P(string key) => new()
        {
            ValidIssuer = Jwt.Issuer, ValidAudience = Jwt.Audience, ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
        var handler = new JwtSecurityTokenHandler();
        Should.NotThrow(() => handler.ValidateToken(token, P(Jwt.SecurityKey), out _));
        Should.Throw<SecurityTokenException>(() => handler.ValidateToken(token, P("a-completely-different-key-0123456789-xyz"), out _));
    }

    [Fact]
    public void AccessToken_EachTokenHasUniqueJti()
    {
        var f = new TokenFunctions(Options.Create(Jwt));
        var u = User();
        var a = new JwtSecurityTokenHandler().ReadJwtToken(f.CreateAccessToken(u).accessToken).Id;
        var b = new JwtSecurityTokenHandler().ReadJwtToken(f.CreateAccessToken(u).accessToken).Id;
        a.ShouldNotBe(b);
    }

    [Fact]
    public void ValidateNewPassword_AcceptsStrongMatchingPassword()
        => ApplicationValidationFunctions.ValidateNewPassword("Str0ng#Passw0rd", "Str0ng#Passw0rd", "t").Status.ShouldBeTrue();

    [Fact]
    public void ValidateNewPassword_RejectsMismatch()
    {
        var r = ApplicationValidationFunctions.ValidateNewPassword("Str0ng#Passw0rd", "Str0ng#Passw0rdX", "t");
        r.Status.ShouldBeFalse();
        r.HttpStatusCode.ShouldBe("400");
        r.Message.ShouldContain(m => m.Contains("do not match"));
    }

    [Fact]
    public void ValidateNewPassword_ListsAllPolicyViolations()
    {
        var r = ApplicationValidationFunctions.ValidateNewPassword("abc", "abc", "t");
        r.Status.ShouldBeFalse();
        r.Message.Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void ValidateUserIdentity_RejectsBadUsernameEmailAndMobile()
    {
        var r = ApplicationValidationFunctions.ValidateUserIdentity("x'; drop", "nope", "abc", "t");
        r.Status.ShouldBeFalse();
        r.Message.Count.ShouldBe(3);
        ApplicationValidationFunctions.ValidateUserIdentity("rahul.k", "rahul@sage.co", "+91 98765 43210", "t").Status.ShouldBeTrue();
    }

    [Fact]
    public void AuthSettings_DefaultsAreSecure()
    {
        var s = new AuthSettings();
        s.MaxFailedLoginAttempts.ShouldBe(5);
        s.LockoutMinutes.ShouldBe(15);
        s.PasswordHashIterations.ShouldBeGreaterThanOrEqualTo(600_000);
        s.RefreshCookieSecure.ShouldBeTrue();
        s.PasswordResetMinutes.ShouldBeLessThanOrEqualTo(60);
    }
}
