using SageAdminApi.AppValidations;
using Shouldly;

namespace SageAdminApi.Tests.UnitTests.Groups;

public class GroupValidationUnitTests
{
    [Theory]
    [InlineData("HR", "Human Resources", null, true)]
    [InlineData("Sales_Manager-2", "Sales Manager", "Leads the sales team", true)]
    [InlineData("Sales.Manager", "x", null, false)]
    [InlineData("2Sales", "x", null, false)]
    [InlineData("", "x", null, false)]
    [InlineData("HR", "", null, false)]
    [InlineData("HR", "   ", null, false)]
    public void ValidateGroup(string code, string name, string? description, bool ok)
        => ApplicationValidationFunctions.ValidateGroup(name, code, description, "t").Status.ShouldBe(ok);

    [Fact]
    public void ValidateGroup_LengthLimits_ReturnsAllErrorsAtOnce()
    {
        ApplicationValidationFunctions.ValidateGroup(new string('n', 100), "HR", new string('d', 500), "t").Status.ShouldBeTrue();
        var r = ApplicationValidationFunctions.ValidateGroup(new string('n', 101), "bad code", new string('d', 501), "t");
        r.Status.ShouldBeFalse();
        r.Message.Count.ShouldBe(3);
        r.HttpStatusCode.ShouldBe("400");
    }

    [Theory]
    [InlineData("Name", null, "Active", true)]
    [InlineData("Name", "d", "Inactive", true)]
    [InlineData("Name", null, "Locked", false)]
    [InlineData("Name", null, "active", false)]
    [InlineData("Name", null, null, false)]
    [InlineData("", null, "Active", false)]
    public void ValidateGroupUpdate(string name, string? description, string? status, bool ok)
        => ApplicationValidationFunctions.ValidateGroupUpdate(name, description, status, "t").Status.ShouldBe(ok);
}
