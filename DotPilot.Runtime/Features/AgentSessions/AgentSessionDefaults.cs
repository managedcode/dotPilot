using DotPilot.Core.Features.AgentSessions;

namespace DotPilot.Runtime.Features.AgentSessions;

public static class AgentSessionDefaults
{
    public const string SystemAgentName = "dotPilot System Agent";
    public const string SystemAgentDescription = "Built-in local agent for desktop chat, prompt-to-agent drafting, and deterministic fallback workflows.";
    public const string SystemAgentPrompt =
        "You are the built-in dotPilot system agent. Help the operator create and run local agents, be explicit about what you are doing, stream visible progress, and stay deterministic when the debug provider is active.";

    public const string WebCapability = "Web";
    public const string ShellCapability = "Shell";
    public const string GitCapability = "Git";
    public const string FilesCapability = "Files";

    public static IReadOnlyList<string> SystemCapabilities { get; } =
    [
        WebCapability,
        ShellCapability,
        GitCapability,
        FilesCapability,
    ];

    public static IReadOnlyList<string> AllCapabilities => SystemCapabilities;

    public static bool IsSystemAgent(string agentName)
    {
        return string.Equals(agentName, SystemAgentName, StringComparison.Ordinal);
    }

    public static string GetDefaultModel(AgentProviderKind kind)
    {
        return AgentSessionProviderCatalog.Get(kind).DefaultModelName;
    }

    public static string CreateAgentDescription(string systemPrompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);

        var normalized = string.Join(
            ' ',
            systemPrompt
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var sentenceEndIndex = normalized.IndexOf('.', StringComparison.Ordinal);
        if (sentenceEndIndex <= 0)
        {
            return normalized;
        }

        return normalized[..(sentenceEndIndex + 1)];
    }
}
