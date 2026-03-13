namespace DotPilot.Presentation;

public sealed class MainViewModel
{
    public MainViewModel(IRuntimeFoundationCatalog runtimeFoundationCatalog)
    {
        ArgumentNullException.ThrowIfNull(runtimeFoundationCatalog);
        RuntimeFoundation = runtimeFoundationCatalog.GetSnapshot();
    }

    public string Title { get; } = "Design Automation Agent";

    public string StatusSummary { get; } = "3 members · GPT-4o";

    public RuntimeFoundationSnapshot RuntimeFoundation { get; }

    public IReadOnlyList<SidebarChatItem> RecentChats { get; } =
    [
        new("Design Automation", "Generate a landing page for our...", true),
        new("Analytics Agent", "Q3 shows 23% growth in 2026 in s...", false),
        new("Code Review Bot", "Found 3 potential issues in this cod...", false),
    ];

    public IReadOnlyList<ChatMessageItem> Messages { get; } =
    [
        new(
            "Design Agent",
            "10:22 AM",
            "Hello! I'm your Design Automation Agent. I can help you create wireframes, generate UI components, write pixel-perfect CSS, and automate your design workflow. What would you like to build today?",
            "D",
            DesignBrushPalette.DesignAvatarBrush,
            false),
        new(
            "Jordan Lee",
            "10:25 AM",
            "@design_agent, generate a landing page for our new AI product. Use a dark theme with accent colors, clean typography, and include a hero section with an animated CTA button.",
            "J",
            DesignBrushPalette.UserAvatarBrush,
            true),
        new(
            "Sarah Kim",
            "10:27 AM",
            "We also have to build a design system for this project.",
            "S",
            DesignBrushPalette.AnalyticsAvatarBrush,
            false),
    ];

    public IReadOnlyList<ParticipantItem> Members { get; } =
    [
        new("Jordan Lee", "@jordan · you", "J", DesignBrushPalette.UserAvatarBrush, "Admin", DesignBrushPalette.AccentBrush),
        new("Sarah Kim", "@sarahk", "S", DesignBrushPalette.AnalyticsAvatarBrush, "Member", DesignBrushPalette.BadgeSurfaceBrush),
        new("Marcus Chen", "@marcus", "M", DesignBrushPalette.CodeAvatarBrush, "Member", DesignBrushPalette.BadgeSurfaceBrush),
    ];

    public IReadOnlyList<ParticipantItem> Agents { get; } =
    [
        new("Design Agent", "GPT-4o · v2.1", "D", DesignBrushPalette.DesignAvatarBrush),
    ];
}
