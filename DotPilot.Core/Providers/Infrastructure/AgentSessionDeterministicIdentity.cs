using System.Security.Cryptography;
using System.Text;
using DotPilot.Core.ControlPlaneDomain;

namespace DotPilot.Core.Providers;

internal static class AgentSessionDeterministicIdentity
{
    private const string ProviderSeedPrefix = "agent-session-provider";
    private const string SeedSeparator = "|";

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
