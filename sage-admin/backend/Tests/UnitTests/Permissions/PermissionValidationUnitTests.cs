using SageAdminApi.AppValidations;
using SageCommonLib.Models.Paging;
using Shouldly;

namespace SageAdminApi.Tests.UnitTests.Permissions;

public class PermissionValidationUnitTests
{
    [Theory]
    [InlineData("Employee", "Employee Management", "HR", true)]
    [InlineData("Hr-Core_2", "Name", null, true)]
    [InlineData("Employee.Core", "Name", null, false)]
    [InlineData("E", "Name", null, false)]
    [InlineData("Employee", "", null, false)]
    [InlineData("Employee", "   ", null, false)]
    public void ValidateModule(string code, string name, string? category, bool ok)
        => ApplicationValidationFunctions.ValidateModule(code, name, category, null, "t").Status.ShouldBe(ok);

    [Fact]
    public void ValidateModule_LengthLimits()
    {
        ApplicationValidationFunctions.ValidateModule("Code", new string('n', 100), new string('c', 100), null, "t").Status.ShouldBeTrue();
        ApplicationValidationFunctions.ValidateModule("Code", new string('n', 101), null, null, "t").Status.ShouldBeFalse();
        ApplicationValidationFunctions.ValidateModule("Code", "n", new string('c', 101), null, "t").Status.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("/products", true)]
    [InlineData("/reports/sales-summary", true)]
    [InlineData("products", false)]
    [InlineData("/Products", false)]
    [InlineData("/products/", false)]
    [InlineData("//x", false)]
    [InlineData("/a b", false)]
    [InlineData("https://evil.example.com", false)]
    public void ValidateModule_MenuLink(string? menuPath, bool ok)
        => ApplicationValidationFunctions.ValidateModule("Code", "Name", null, menuPath, "t").Status.ShouldBe(ok);

    [Theory]
    [InlineData("View", "Employee - View", null, true)]
    [InlineData("Export", "Export", "Allows export", true)]
    [InlineData("Bad.Action", "x", null, false)]
    [InlineData("", "x", null, false)]
    [InlineData("View", "", null, false)]
    public void ValidatePermission(string action, string name, string? description, bool ok)
        => ApplicationValidationFunctions.ValidatePermission(action, name, description, "t").Status.ShouldBe(ok);

    [Fact]
    public void ValidatePermission_LengthLimits_ReturnsAllErrorsAtOnce()
    {
        var r = ApplicationValidationFunctions.ValidatePermission("Bad.Action", new string('n', 151), new string('d', 501), "t");
        r.Status.ShouldBeFalse();
        r.Message.Count.ShouldBe(3);
        r.HttpStatusCode.ShouldBe("400");
    }

    [Fact]
    public void Pagination_SortByIsRestrictedToTheAllowList()
    {
        var allowed = new[] { "code", "name" };
        var p = new Pagination();

        p.GetPagingParams(new HttpPaging { SortBy = "NAME", SortDirection = "asc" }, allowed, "code").SortBy.ShouldBe("name");
        p.GetPagingParams(new HttpPaging { SortBy = "name; DROP TABLE x" }, allowed, "code").SortBy.ShouldBe("code");
        p.GetPagingParams(new HttpPaging { SortBy = "" }, allowed, "code").SortDirection.ShouldBe("DESC");
        p.GetPagingParams(new HttpPaging { SortDirection = "asc" }, allowed, "code").SortDirection.ShouldBe("ASC");
        p.GetPagingParams(new HttpPaging { SortDirection = "sideways; --" }, allowed, "code").SortDirection.ShouldBe("DESC");
    }
}
