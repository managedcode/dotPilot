using System.Globalization;
using DotPilot.Core.Providers;
using Microsoft.Extensions.Logging;

namespace DotPilot.Core.AgentBuilder;

public sealed class AgentPromptDraftGenerator(
    IAgentProviderStatusReader providerStatusReader,
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
        var provider = await ResolvePreferredProviderAsync(ManualPrompt, cancellationToken);
        var draft = new AgentPromptDraft(
            ManualPrompt,
            ManualAgentName,
            ManualDescription,
            provider.Kind,
            provider.SuggestedModelName,
            CreateSystemPrompt(ManualAgentName, ManualDescription));

        AgentPromptDraftGeneratorLog.ManualDraftCreated(logger, draft.ProviderKind, draft.ModelName);
        return draft;
    }

    public async ValueTask<AgentPromptDraft> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var normalizedPrompt = NormalizePrompt(prompt);
        var provider = await ResolvePreferredProviderAsync(normalizedPrompt, cancellationToken);
        var description = CreateDescription(normalizedPrompt);
        var name = CreateName(normalizedPrompt);
        var draft = new AgentPromptDraft(
            normalizedPrompt,
            name,
            description,
            provider.Kind,
            provider.SuggestedModelName,
            CreateSystemPrompt(name, description));

        AgentPromptDraftGeneratorLog.GeneratedDraft(
            logger,
            draft.Name,
            draft.ProviderKind,
            draft.ModelName);
        return draft;
    }

    private async ValueTask<ProviderStatusDescriptor> ResolvePreferredProviderAsync(string prompt, CancellationToken cancellationToken)
    {
        var providers = await providerStatusReader.ReadAsync(cancellationToken);
        var creatableProviders = providers
            .Where(static provider => provider.CanCreateAgents)
            .ToDictionary(static provider => provider.Kind);

        foreach (var candidate in ResolveProviderPreferences(prompt))
        {
            if (creatableProviders.TryGetValue(candidate, out var provider))
            {
                return provider;
            }
        }

        var firstCreatableRealProvider = providers.FirstOrDefault(static provider =>
            provider.CanCreateAgents &&
            provider.Kind != AgentProviderKind.Debug);
        if (firstCreatableRealProvider is not null)
        {
            return firstCreatableRealProvider;
        }

        return providers.FirstOrDefault(static provider => provider.Kind == AgentProviderKind.Debug)
            ?? new ProviderStatusDescriptor(
                AgentSessionDeterministicIdentity.CreateProviderId("debug"),
                AgentProviderKind.Debug,
                "Debug Provider",
                "debug",
                AgentProviderStatus.Disabled,
                "Provider is disabled for local agent creation.",
                AgentSessionDefaults.GetDefaultModel(AgentProviderKind.Debug),
                [AgentSessionDefaults.GetDefaultModel(AgentProviderKind.Debug)],
                AgentSessionDefaults.GetDefaultModel(AgentProviderKind.Debug),
                false,
                false,
                [],
                []);
    }

    private static IEnumerable<AgentProviderKind> ResolveProviderPreferences(string prompt)
    {
        if (ContainsAny(prompt, "review", "pull request", "repository", "repo", "code", "commit", "branch", "diff"))
        {
            yield return AgentProviderKind.Codex;
            yield return AgentProviderKind.GitHubCopilot;
            yield return AgentProviderKind.Gemini;
            yield return AgentProviderKind.ClaudeCode;
            yield return AgentProviderKind.Onnx;
            yield return AgentProviderKind.LlamaSharp;
            yield return AgentProviderKind.Debug;
            yield break;
        }

        if (ContainsAny(prompt, "research", "search", "summarize", "summary", "writing", "content", "docs", "analysis"))
        {
            yield return AgentProviderKind.ClaudeCode;
            yield return AgentProviderKind.Gemini;
            yield return AgentProviderKind.Codex;
            yield return AgentProviderKind.GitHubCopilot;
            yield return AgentProviderKind.Onnx;
            yield return AgentProviderKind.LlamaSharp;
            yield return AgentProviderKind.Debug;
            yield break;
        }

        yield return AgentProviderKind.Codex;
        yield return AgentProviderKind.Gemini;
        yield return AgentProviderKind.ClaudeCode;
        yield return AgentProviderKind.GitHubCopilot;
        yield return AgentProviderKind.Onnx;
        yield return AgentProviderKind.LlamaSharp;
        yield return AgentProviderKind.Debug;
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

    private static string CreateName(string prompt)
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
            return ManualAgentName;
        }

        var baseName = string.Join(" ", nameWords);
        return baseName.EndsWith("Agent", StringComparison.Ordinal)
            ? baseName
            : string.Create(CultureInfo.InvariantCulture, $"{baseName} Agent");
    }

    private static string CreateSystemPrompt(
        string name,
        string description)
    {
        return AgentSessionDefaults.CreateStructuredSystemPrompt(name, description);
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
        Message = "Generated prompt-based agent draft. Name={AgentName} Provider={ProviderKind} Model={ModelName}.")]
    public static partial void GeneratedDraft(
        ILogger logger,
        string agentName,
        AgentProviderKind providerKind,
        string modelName);
}
