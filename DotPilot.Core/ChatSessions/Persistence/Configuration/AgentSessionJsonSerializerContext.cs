using System.Text.Json.Serialization;

namespace DotPilot.Core.ChatSessions;

[JsonSerializable(typeof(string[]))]
internal sealed partial class AgentSessionJsonSerializerContext : JsonSerializerContext
{
}
