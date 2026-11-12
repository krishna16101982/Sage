using SageCommonLib.Models;
using SageCommonLib.Models.Paging;
using Shouldly;

namespace SageCommonLib.Tests.Models;

public class ModelTests
{
    [Fact]
    public void Pagination_ClampsInvalidValues()
    {
        var p = new PaginationParams { Page = -3, PageSize = 100000 };
        p.Page.ShouldBe(1);
        p.PageSize.ShouldBe(PaginationParams.MaxPageSize);
        new PaginationParams { Page = 3, PageSize = 10 }.Offset.ShouldBe(20);
    }

    [Fact]
    public void PagedResult_ComputesTotalPages()
        => new PagedResult<int> { PageSize = 10, TotalCount = 21 }.TotalPages.ShouldBe(3);

    [Fact]
    public void ApiResponse_SuccessAndError_HaveConsistentEnvelope()
    {
        var api = new ApiResponse();
        var ok = api.Success(new { a = 1 });
        ok.Status.ShouldBeTrue(); ok.HttpStatus.ShouldBe("200");
        var err = api.Error("X", new[] { "bad" }, "400");
        err.Status.ShouldBeFalse(); err.Data.ShouldBeNull(); err.Message.ShouldBe(new[] { "bad" });
    }
}
