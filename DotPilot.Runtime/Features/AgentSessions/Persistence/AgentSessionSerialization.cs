using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace DotPilot.Runtime.Features.AgentSessions;

internal static class AgentSessionSerialization
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            WriteIndented = false,
        };
    }
}
