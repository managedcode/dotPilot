namespace DotPilot.Presentation;

public sealed class SecondViewModel
{
    public SecondViewModel(IRuntimeFoundationCatalog runtimeFoundationCatalog)
    {
        ArgumentNullException.ThrowIfNull(runtimeFoundationCatalog);
        RuntimeFoundation = runtimeFoundationCatalog.GetSnapshot();
    }

    public string PageTitle { get; } = "Create New Agent";

    public string PageSubtitle { get; } = "Configure your AI agent's capabilities, model, and behavior";

    public RuntimeFoundationSnapshot RuntimeFoundation { get; }

    public string SystemPrompt { get; } =
        """
        You are a helpful AI assistant. Your role is to...

        Key behaviors:
        • Be concise, clear, and accurate
        • Ask clarifying questions when requirements are ambiguous
        • Always cite sources when providing facts or statistics
        • Format responses using markdown when appropriate
        """;

    public string TokenSummary { get; } = "0 / 4,096 tokens";

    public IReadOnlyList<AgentMenuItem> ExistingAgents { get; } =
    [
        new("Design Agent", "Claude 3.5 · v1.0", "D", DesignBrushPalette.DesignAvatarBrush),
        new("Code Agent", "GPT-4o", "C", DesignBrushPalette.CodeAvatarBrush),
        new("Analytics Agent", "Claude 3.5 · v1.0", "A", DesignBrushPalette.AnalyticsAvatarBrush),
    ];

    public IReadOnlyList<AgentTypeOption> AgentTypes { get; } =
    [
        new("Assistant", true),
        new("Analyst", false),
        new("Executor", false),
        new("Orchestrator", false),
    ];

    public IReadOnlyList<AvatarOption> AvatarOptions { get; } =
    [
        new("A", DesignBrushPalette.DesignAvatarBrush),
        new("B", DesignBrushPalette.CodeAvatarBrush),
        new("C", DesignBrushPalette.AnalyticsAvatarBrush),
        new("D", DesignBrushPalette.AvatarVariantDanishBrush),
        new("E", DesignBrushPalette.AvatarVariantEmilyBrush),
        new("F", DesignBrushPalette.AvatarVariantFrankBrush),
    ];

    public IReadOnlyList<string> PromptTemplates { get; } =
    [
        "Research assistant",
        "Customer support specialist",
        "Code review expert",
    ];

    public IReadOnlyList<SkillToggleItem> Skills { get; } =
    [
        new("Web Search", "Search the internet for current information and news", "⌘", true),
        new("Code Execution", "Run Python, JavaScript, and shell scripts in sandbox", "</>", true),
        new("File Analysis", "Read, parse, and summarize uploaded documents", "▣", false),
        new("Database Access", "Query and modify SQL/NoSQL database records", "◫", false),
        new("API Calls", "Connect to external REST APIs and webhooks", "⇄", true),
    ];
}
