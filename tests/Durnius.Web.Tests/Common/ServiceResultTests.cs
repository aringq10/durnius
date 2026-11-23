using Durnius.Web.Common;
using Xunit;

namespace Durnius.Web.Tests.Common;

public sealed class ServiceResultTests
{
    private enum TestError
    {
        Missing
    }

    [Fact]
    public void MatchCallsSuccessHandlerAndConvertsValue()
    {
        var result = ServiceResult<int, TestError>.Success(42);

        var message = result.Match(
            value => $"Value: {value}",
            (_, _) => "Failed");

        Assert.Equal("Value: 42", message);
    }

    [Fact]
    public void MatchCallsFailureHandlerAndPreservesMessages()
    {
        var result = ServiceResult<int, TestError>.Failure(
            TestError.Missing,
            ["The requested value was not found."]);

        var message = result.Match(
            value => $"Value: {value}",
            (error, messages) => $"{error}: {string.Join(" ", messages)}");

        Assert.Equal("Missing: The requested value was not found.", message);
    }
}
