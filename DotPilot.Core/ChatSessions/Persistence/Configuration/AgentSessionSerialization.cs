using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace DotPilot.Core.ChatSessions;

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
