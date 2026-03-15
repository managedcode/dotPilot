using ManagedCode.Communication;
namespace DotPilot.Tests;

internal static class TestResultExtensions
{
    public static T ShouldSucceed<T>(this Result<T> result)
    {
        result.IsSuccess.Should().BeTrue(result.ToDisplayMessage("Operation should succeed."));
        return result.Value!;
    }

    public static void ShouldSucceed(this Result result)
    {
        result.IsSuccess.Should().BeTrue(result.ToDisplayMessage("Operation should succeed."));
    }
}
