using System.Security.Cryptography;
using System.Text;
using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Runtime.Features.ToolchainCenter;

internal static class ToolchainDeterministicIdentity
{
    public static ProviderId CreateProviderId(string commandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(commandName));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, guidBytes.Length).CopyTo(guidBytes);

        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new ProviderId(new Guid(guidBytes));
    }
}
