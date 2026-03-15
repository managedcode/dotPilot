using System.Text.Json.Serialization;

namespace DotPilot.Runtime.Features.AgentSessions;

[JsonSerializable(typeof(string[]))]
internal sealed partial class AgentSessionJsonSerializerContext : JsonSerializerContext
{
}
