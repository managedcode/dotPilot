using ManagedCode.Communication;
using ManagedCode.Communication.Results.Extensions;

namespace DotPilot.Presentation;

internal static class OperationResultExtensions
{
    public static bool TryGetValue<T>(this Result<T> result, out T value)
    {
        if (result.IsSuccess)
        {
            value = result.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public static string ToOperatorMessage(this IResultProblem result, string fallbackMessage)
    {
        return result.ToDisplayMessage(fallbackMessage);
    }
}
