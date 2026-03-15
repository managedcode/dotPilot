namespace DotPilot.Core.ChatSessions;

internal static class AgentProfileSchemaDefaults
{
    // Legacy SQLite stores still require these columns even though the current UI no longer edits them.
    public const int DefaultRole = 4;

    public const string EmptyCapabilitiesJson = "[]";
}
