namespace DotPilot.Presentation;

public sealed record SidebarChatItem(string Title, string Preview, bool IsSelected);

public sealed record ChatMessageItem(
    string Author,
    string Timestamp,
    string Content,
    string Initial,
    Brush? AvatarBrush,
    bool IsCurrentUser);

public sealed record ParticipantItem(
    string Name,
    string SecondaryText,
    string Initial,
    Brush? AvatarBrush,
    string? BadgeText = null,
    Brush? BadgeBrush = null);
