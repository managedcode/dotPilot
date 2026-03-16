using System.Globalization;
using System.Text;
using DotPilot.Core.Providers;

namespace DotPilot.Core.AgentBuilder;

public static class AgentSessionDefaults
{
    public const string SystemAgentName = "dotPilot System Agent";
    public const string SystemAgentDescription = "Built-in local agent for desktop chat, prompt-to-agent drafting, and deterministic fallback workflows.";

    private const string MissionPrefix = "Mission:";
    private const string StructuredPromptClosingInstruction =
        "Be explicit about actions and keep the operator informed.";

    public static string SystemAgentPrompt { get; } =
        CreateStructuredSystemPrompt(
            SystemAgentName,
            SystemAgentDescription);

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

        foreach (var line in systemPrompt.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith(MissionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return line[MissionPrefix.Length..].Trim();
            }
        }

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

    public static string CreateStructuredSystemPrompt(
        string name,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var normalizedDescription = description.Trim();
        if (!normalizedDescription.EndsWith('.'))
        {
            normalizedDescription += ".";
        }

        StringBuilder builder = new();
        builder.AppendLine(CultureInfo.InvariantCulture, $"You are {name}.");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{MissionPrefix} {normalizedDescription}");
        builder.Append(StructuredPromptClosingInstruction);
        return builder.ToString().Trim();
    }
}
