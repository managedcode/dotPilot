using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotPilot.Presentation;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(OperatorPreferencesDto))]
internal sealed partial class OperatorPreferencesJsonSerializerContext : JsonSerializerContext
{
}
