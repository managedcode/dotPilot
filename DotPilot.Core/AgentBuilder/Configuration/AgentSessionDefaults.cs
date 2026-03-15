using System.Globalization;
using System.Text;
using DotPilot.Core.Providers;

namespace DotPilot.Core.AgentBuilder;

public static class AgentSessionDefaults
{
    public const string SystemAgentName = "dotPilot System Agent";
    public const string SystemAgentDescription = "Built-in local agent for desktop chat, prompt-to-agent drafting, and deterministic fallback workflows.";

    public const string WebCapability = "Web";
    public const string ShellCapability = "Shell";
    public const string GitCapability = "Git";
    public const string FilesCapability = "Files";
    public const string TaskPlanningSkill = "Task planning";
    public const string OperatorUpdatesSkill = "Operator updates";
    public const string RepositoryReviewSkill = "Repository review";
    public const string ChangeExplanationSkill = "Change explanation";
    public const string CodeEditingSkill = "Code editing";
    public const string DocumentationResearchSkill = "Documentation research";
    public const string ResearchSynthesisSkill = "Research synthesis";
    private const string SkillTagPrefix = "skill:";
    private const string MissionPrefix = "Mission:";
    private const string PrimaryToolsPrefix = "Primary tools:";
    private const string PreferredSkillsPrefix = "Preferred skills:";
    private const string StructuredPromptClosingInstruction =
        "Be explicit about actions, keep the operator informed, and stay within the selected tools and skills.";

    public static IReadOnlyList<string> SystemTools { get; } =
    [
        WebCapability,
        ShellCapability,
        GitCapability,
        FilesCapability,
    ];

    public static IReadOnlyList<string> SystemSkills { get; } =
    [
        TaskPlanningSkill,
        OperatorUpdatesSkill,
    ];

    public static string SystemAgentPrompt { get; } =
        CreateStructuredSystemPrompt(
            SystemAgentName,
            SystemAgentDescription,
            SystemTools,
            SystemSkills);

    public static IReadOnlyList<string> SystemCapabilities => EncodeSelections(SystemTools, SystemSkills);

    public static IReadOnlyList<string> AllTools => SystemTools;

    public static IReadOnlyList<string> AllSkills { get; } =
    [
        TaskPlanningSkill,
        OperatorUpdatesSkill,
        RepositoryReviewSkill,
        ChangeExplanationSkill,
        CodeEditingSkill,
        DocumentationResearchSkill,
        ResearchSynthesisSkill,
    ];

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
        string description,
        IReadOnlyList<string> tools,
        IReadOnlyList<string> skills)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(skills);

        var normalizedDescription = description.Trim();
        if (!normalizedDescription.EndsWith('.'))
        {
            normalizedDescription += ".";
        }

        StringBuilder builder = new();
        builder.AppendLine(CultureInfo.InvariantCulture, $"You are {name}.");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{MissionPrefix} {normalizedDescription}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{PrimaryToolsPrefix} {JoinValuesOrFallback(tools, FilesCapability)}.");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{PreferredSkillsPrefix} {JoinValuesOrFallback(skills, TaskPlanningSkill)}.");
        builder.Append(StructuredPromptClosingInstruction);
        return builder.ToString().Trim();
    }

    public static IReadOnlyList<string> EncodeSelections(
        IReadOnlyList<string> tools,
        IReadOnlyList<string> skills)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(skills);

        List<string> encoded = [];
        HashSet<string> seen = [];

        AddUnique(encoded, seen, tools);
        AddUnique(encoded, seen, skills.Select(EncodeSkillTag));
        return encoded;
    }

    public static IReadOnlyList<string> DecodeTools(IReadOnlyList<string> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);

        return selections
            .Where(static selection => !IsSkillTag(selection))
            .ToArray();
    }

    public static IReadOnlyList<string> DecodeSkills(IReadOnlyList<string> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);

        return selections
            .Where(static selection => IsSkillTag(selection))
            .Select(DecodeSkillTag)
            .ToArray();
    }

    public static IReadOnlyList<string> CreateVisibleTags(IReadOnlyList<string> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);

        return selections
            .Select(static selection => IsSkillTag(selection) ? DecodeSkillTag(selection) : selection)
            .ToArray();
    }

    public static string GetToolDescription(string tool)
    {
        return tool switch
        {
            WebCapability => "Browse the web and inspect external documentation.",
            ShellCapability => "Run terminal commands inside the assigned workspace.",
            GitCapability => "Inspect repository status, diffs, branches, and history.",
            FilesCapability => "Read and update local files in the assigned workspace.",
            _ => "Use the selected tool only when it helps the task."
        };
    }

    public static string GetSkillDescription(string skill)
    {
        return skill switch
        {
            TaskPlanningSkill => "Break the task into clear ordered steps before execution.",
            OperatorUpdatesSkill => "Keep the operator informed with concise progress updates.",
            RepositoryReviewSkill => "Inspect repository state and review changes before acting.",
            ChangeExplanationSkill => "Explain findings, diffs, and decisions in plain language.",
            CodeEditingSkill => "Implement and refine code changes safely.",
            DocumentationResearchSkill => "Read docs and extract only the relevant guidance.",
            ResearchSynthesisSkill => "Summarize findings into a usable answer or plan.",
            _ => "Apply this skill when it strengthens the result."
        };
    }

    private static string JoinValuesOrFallback(IReadOnlyList<string> values, string fallbackValue)
    {
        return values.Count == 0
            ? fallbackValue
            : string.Join(", ", values);
    }

    private static string EncodeSkillTag(string skill)
    {
        return SkillTagPrefix + skill;
    }

    private static bool IsSkillTag(string selection)
    {
        return selection.StartsWith(SkillTagPrefix, StringComparison.Ordinal);
    }

    private static string DecodeSkillTag(string selection)
    {
        return selection[SkillTagPrefix.Length..];
    }

    private static void AddUnique(
        List<string> encoded,
        HashSet<string> seen,
        IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
            {
                continue;
            }

            encoded.Add(candidate);
        }
    }
}
