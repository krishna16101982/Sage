using SageCommonLib.AppValidations;
using Shouldly;

namespace SageCommonLib.Tests.AppValidations;

public class ValidationFunctionsTests
{
    [Theory]
    [InlineData("a@b.com", true)]
    [InlineData("rahul.k@sage.co.in", true)]
    [InlineData("no-at.com", false)]
    [InlineData("a@b", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Email(string? v, bool ok) => ValidationFunctions.IsValidEmail(v).ShouldBe(ok);

    [Theory]
    [InlineData("rahul", true)]
    [InlineData("a.b-c_1", true)]
    [InlineData("ab", false)]
    [InlineData("has space", false)]
    [InlineData("x'; drop", false)]
    public void Username(string v, bool ok) => ValidationFunctions.IsValidUsername(v).ShouldBe(ok);

    [Theory]
    [InlineData("+91 98765 43210", true)]
    [InlineData("9876543210", true)]
    [InlineData("98765-43210", true)]
    [InlineData("09876543210", true)]
    [InlineData("+919876543210", true)]
    [InlineData("98765432101", false)]     // 11 digits
    [InlineData("987654321", false)]       // 9 digits
    [InlineData("+44 7911 123456", false)] // other country codes
    [InlineData("98765 4321a", false)]
    [InlineData("abc", false)]
    public void Phone(string v, bool ok) => ValidationFunctions.IsValidPhone(v).ShouldBe(ok);

    [Fact]
    public void Password_Strong_HasNoErrors() => ValidationFunctions.CheckPasswordStrength("Str0ng#Passw0rd").ShouldBeEmpty();

    [Fact]
    public void Password_Weak_ListsEveryViolation()
        => ValidationFunctions.CheckPasswordStrength("abc").Count.ShouldBe(4);

    [Fact]
    public void EscapeLike_EscapesWildcards() => "50%_[x]".EscapeLike().ShouldBe(@"50\%\_\[x]");

    [Fact]
    public void ForLog_StripsNewlines() => "a\r\nb".ForLog().ShouldNotContain("\n");
}
