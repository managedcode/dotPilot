namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

internal static class EmbeddedRuntimeGrainGuards
{
    public static void EnsureMatchingKey(string descriptorId, string grainKey, string grainName)
    {
        if (string.Equals(descriptorId, grainKey, StringComparison.Ordinal))
        {
            return;
        }

        throw new ArgumentException(
            string.Concat(EmbeddedRuntimeHostNames.MismatchedPrimaryKeyPrefix, grainName),
            nameof(descriptorId));
    }
}
