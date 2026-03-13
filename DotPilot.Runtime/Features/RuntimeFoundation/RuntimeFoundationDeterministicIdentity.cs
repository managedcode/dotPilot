using System.Security.Cryptography;
using System.Text;
using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Runtime.Features.RuntimeFoundation;

internal static class RuntimeFoundationDeterministicIdentity
{
    private const string ArtifactSeedPrefix = "runtime-foundation-artifact";
    private const string ProviderSeedPrefix = "runtime-foundation-provider";
    private const string SeedSeparator = "|";

    public static DateTimeOffset ArtifactCreatedAt { get; } = new(2026, 3, 13, 0, 0, 0, TimeSpan.Zero);

    public static ArtifactId CreateArtifactId(SessionId sessionId, string artifactName)
    {
        return new(CreateGuid(string.Concat(ArtifactSeedPrefix, SeedSeparator, sessionId, SeedSeparator, artifactName)));
    }

    public static ProviderId CreateProviderId(string commandName)
    {
        return new(CreateGuid(string.Concat(ProviderSeedPrefix, SeedSeparator, commandName)));
    }

    private static Guid CreateGuid(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        Span<byte> guidBytes = stackalloc byte[16];
        hash[..guidBytes.Length].CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x80);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
