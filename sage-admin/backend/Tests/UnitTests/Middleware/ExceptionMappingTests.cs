using SageAdminApi.AppFunctions;
using SageCommonLib.Models;
using Shouldly;

namespace SageAdminApi.Tests.UnitTests.Middleware;

public class ExceptionMappingTests
{
    private static (int status, ApiResponse<object> body) Map(Exception ex)
    {
        var m = typeof(ExceptionHandlingMiddleware).GetMethod("Build", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        return ((int, ApiResponse<object>))m.Invoke(null, new object[] { ex, new ApiResponse() })!;
    }

    public static IEnumerable<object[]> Known() => new[]
    {
        new object[] { new NotFoundException("m"), 404 },
        new object[] { new ConflictException("m"), 409 },
        new object[] { new ForbiddenException(), 403 },
        new object[] { new UnauthorizedAppException(), 401 },
    };

    [Theory]
    [MemberData(nameof(Known))]
    public void KnownExceptions_MapToExpectedStatus(Exception ex, int expected)
    {
        var (status, body) = Map(ex);
        status.ShouldBe(expected);
        body.Status.ShouldBeFalse();
    }

    [Fact]
    public void ValidationException_Returns400WithAllErrors()
    {
        var (status, body) = Map(new ValidationException(new[] { "a", "b" }));
        status.ShouldBe(400);
        body.Message.ShouldBe(new[] { "a", "b" });
    }

    [Fact]
    public void UnknownException_Returns500_WithGenericMessage()
    {
        var (status, body) = Map(new InvalidOperationException("secret internals"));
        status.ShouldBe(500);
        body.Message.ShouldNotContain(m => m.Contains("secret"));
    }
}
