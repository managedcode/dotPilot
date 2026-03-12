namespace DotPilot.Presentation;

public sealed record AgentMenuItem(string Name, string ModelSummary, string Initial, Brush? AvatarBrush);

public sealed record AgentTypeOption(string Label, bool IsSelected);

public sealed record AvatarOption(string Initial, Brush? AvatarBrush);

public sealed record SkillToggleItem(string Name, string Description, string IconGlyph, bool IsEnabled);
