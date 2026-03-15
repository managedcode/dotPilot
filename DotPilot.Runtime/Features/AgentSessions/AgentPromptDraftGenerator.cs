using System.Globalization;
using System.Text;
using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.Extensions.Logging;

namespace DotPilot.Runtime.Features.AgentSessions;

public sealed record AgentPromptDraft(
    string Prompt,
    string Name,
    string Description,
    AgentRoleKind Role,
    AgentProviderKind ProviderKind,
    string ModelName,
    string SystemPrompt,
    IReadOnlyList<string> Capabilities);

public sealed class AgentPromptDraftGenerator(
    IAgentProviderStatusCache providerStatusCache,
    ILogger<AgentPromptDraftGenerator> logger)
{
    private const string ManualPrompt = "Build manually";
    private const string ManualAgentName = "New agent";
    private const string ManualDescription = "Local desktop assistant for reusable chat and workflow sessions.";
    private const string PromptPrefixCreate = "create ";
    private const string PromptPrefixBuild = "build ";
    private const string PromptPrefixMake = "make ";
    private const string PromptPrefixIWant = "i want ";
    private static readonly string[] PromptPrefixes =
    [
        PromptPrefixCreate,
        PromptPrefixBuild,
        PromptPrefixMake,
        PromptPrefixIWant,
    ];
    private static readonly char[] NameSplitCharacters =
    [
        ' ',
        ',',
        '.',
        ';',
        ':',
        '-',
        '/',
        '\\',
    ];
    private static readonly string[] NoiseTerms =
    [
        "an",
        "a",
        "agent",
        "assistant",
        "that",
        "which",
        "who",
        "for",
        "to",
        "with",
        "the",
        "and",
        "can",
    ];

    public async ValueTask<AgentPromptDraft> CreateManualDraftAsync(CancellationToken cancellationToken)
    {
        var providerKind = await ResolvePreferredProviderAsync(ManualPrompt, cancellationToken);
        var role = AgentRoleKind.Operator;
        var capabilities = ResolveCapabilities(ManualPrompt);
        var draft = new AgentPromptDraft(
            ManualPrompt,
            ManualAgentName,
            ManualDescription,
            role,
            providerKind,
            AgentSessionDefaults.GetDefaultModel(providerKind),
            CreateSystemPrompt(ManualAgentName, ManualDescription, role, capabilities),
            capabilities);

        AgentPromptDraftGeneratorLog.ManualDraftCreated(logger, draft.ProviderKind, draft.ModelName);
        return draft;
    }

    public async ValueTask<AgentPromptDraft> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var normalizedPrompt = NormalizePrompt(prompt);
        var providerKind = await ResolvePreferredProviderAsync(normalizedPrompt, cancellationToken);
        var role = ResolveRole(normalizedPrompt);
        var capabilities = ResolveCapabilities(normalizedPrompt);
        var description = CreateDescription(normalizedPrompt);
        var name = CreateName(normalizedPrompt, role);
        var draft = new AgentPromptDraft(
            normalizedPrompt,
            name,
            description,
            role,
            providerKind,
            AgentSessionDefaults.GetDefaultModel(providerKind),
            CreateSystemPrompt(name, description, role, capabilities),
            capabilities);

        AgentPromptDraftGeneratorLog.GeneratedDraft(
            logger,
            draft.Name,
            draft.ProviderKind,
            draft.ModelName,
            draft.Capabilities.Count);
        return draft;
    }

    private async ValueTask<AgentProviderKind> ResolvePreferredProviderAsync(string prompt, CancellationToken cancellationToken)
    {
        var providers = await providerStatusCache.GetSnapshotAsync(cancellationToken);
        var creatableProviders = providers
            .Where(static provider => provider.CanCreateAgents)
            .Select(static provider => provider.Kind)
            .ToHashSet();

        foreach (var candidate in ResolveProviderPreferences(prompt))
        {
            if (creatableProviders.Contains(candidate))
            {
                return candidate;
            }
        }

        return AgentProviderKind.Debug;
    }

    private static IEnumerable<AgentProviderKind> ResolveProviderPreferences(string prompt)
    {
        if (ContainsAny(prompt, "review", "pull request", "repository", "repo", "code", "commit", "branch", "diff"))
        {
            yield return AgentProviderKind.Codex;
            yield return AgentProviderKind.GitHubCopilot;
            yield return AgentProviderKind.ClaudeCode;
            yield return AgentProviderKind.Debug;
            yield break;
        }

        if (ContainsAny(prompt, "research", "search", "summarize", "summary", "writing", "content", "docs", "analysis"))
        {
            yield return AgentProviderKind.ClaudeCode;
            yield return AgentProviderKind.Codex;
            yield return AgentProviderKind.GitHubCopilot;
            yield return AgentProviderKind.Debug;
            yield break;
        }

        yield return AgentProviderKind.Codex;
        yield return AgentProviderKind.ClaudeCode;
        yield return AgentProviderKind.GitHubCopilot;
        yield return AgentProviderKind.Debug;
    }

    private static AgentRoleKind ResolveRole(string prompt)
    {
        if (ContainsAny(prompt, "review", "audit"))
        {
            return AgentRoleKind.Reviewer;
        }

        if (ContainsAny(prompt, "research", "search", "knowledge"))
        {
            return AgentRoleKind.Research;
        }

        if (ContainsAny(prompt, "code", "repository", "repo", "commit", "refactor"))
        {
            return AgentRoleKind.Coding;
        }

        return AgentRoleKind.Operator;
    }

    private static string[] ResolveCapabilities(string prompt)
    {
        HashSet<string> capabilities =
        [
            AgentSessionDefaults.FilesCapability,
        ];

        if (ContainsAny(prompt, "shell", "terminal", "command", "cli", "script"))
        {
            capabilities.Add(AgentSessionDefaults.ShellCapability);
        }

        if (ContainsAny(prompt, "git", "repository", "repo", "commit", "pull request", "branch", "diff"))
        {
            capabilities.Add(AgentSessionDefaults.GitCapability);
            capabilities.Add(AgentSessionDefaults.FilesCapability);
        }

        if (ContainsAny(prompt, "research", "search", "browse", "web", "internet", "documentation", "docs"))
        {
            capabilities.Add(AgentSessionDefaults.WebCapability);
        }

        if (ContainsAny(prompt, "code", "refactor", "implement"))
        {
            capabilities.Add(AgentSessionDefaults.ShellCapability);
            capabilities.Add(AgentSessionDefaults.GitCapability);
        }

        if (capabilities.Count == 1)
        {
            capabilities.Add(AgentSessionDefaults.ShellCapability);
        }

        return capabilities
            .OrderBy(static capability => capability, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizePrompt(string prompt)
    {
        var normalized = string.Join(
            ' ',
            (prompt ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Describe the agent you want before generating a draft.");
        }

        return normalized;
    }

    private static string CreateDescription(string prompt)
    {
        var normalized = prompt.Trim();
        if (!normalized.EndsWith('.'))
        {
            normalized += ".";
        }

        return normalized;
    }

    private static string CreateName(string prompt, AgentRoleKind role)
    {
        var normalized = prompt.Trim();
        foreach (var prefix in PromptPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..];
                break;
            }
        }

        var nameWords = normalized
            .Split(NameSplitCharacters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => !NoiseTerms.Contains(word, StringComparer.OrdinalIgnoreCase))
            .Take(3)
            .Select(ToTitleCase)
            .ToArray();

        if (nameWords.Length == 0)
        {
            return role switch
            {
                AgentRoleKind.Coding => "Coding Agent",
                AgentRoleKind.Research => "Research Agent",
                AgentRoleKind.Reviewer => "Review Agent",
                _ => ManualAgentName,
            };
        }

        var baseName = string.Join(" ", nameWords);
        return baseName.EndsWith("Agent", StringComparison.Ordinal)
            ? baseName
            : string.Create(CultureInfo.InvariantCulture, $"{baseName} Agent");
    }

    private static string CreateSystemPrompt(
        string name,
        string description,
        AgentRoleKind role,
        IReadOnlyList<string> capabilities)
    {
        var capabilityText = string.Join(", ", capabilities);
        StringBuilder builder = new();
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"You are {name}."));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Role: {role}."));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Mission: {description}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Available capabilities: {capabilityText}."));
        builder.Append("Be explicit about actions, keep the operator informed, and stay within the assigned capabilities.");
        return builder.ToString().Trim();
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToTitleCase(string value)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
    }
}

internal static partial class AgentPromptDraftGeneratorLog
{
    [LoggerMessage(
        EventId = 1400,
        Level = LogLevel.Information,
        Message = "Created manual agent draft. Provider={ProviderKind} Model={ModelName}.")]
    public static partial void ManualDraftCreated(ILogger logger, AgentProviderKind providerKind, string modelName);

    [LoggerMessage(
        EventId = 1401,
        Level = LogLevel.Information,
        Message = "Generated prompt-based agent draft. Name={AgentName} Provider={ProviderKind} Model={ModelName} Capabilities={CapabilityCount}.")]
    public static partial void GeneratedDraft(
        ILogger logger,
        string agentName,
        AgentProviderKind providerKind,
        string modelName,
        int capabilityCount);
}
