namespace DotPilot.Presentation;

internal static class DesignBrushPalette
{
    private const string UserAvatarBrushKey = "UserAvatarBrush";
    private const string DesignAvatarBrushKey = "DesignAvatarBrush";
    private const string CodeAvatarBrushKey = "CodeAvatarBrush";
    private const string AnalyticsAvatarBrushKey = "AnalyticsAvatarBrush";
    private const string AvatarVariantDanishBrushKey = "AvatarVariantDanishBrush";
    private const string AvatarVariantEmilyBrushKey = "AvatarVariantEmilyBrush";
    private const string AvatarVariantFrankBrushKey = "AvatarVariantFrankBrush";
    private const string AppAccentBrushKey = "AppAccentBrush";
    private const string AppBadgeSurfaceBrushKey = "AppBadgeSurfaceBrush";
    public static Brush? UserAvatarBrush => GetBrush(UserAvatarBrushKey);

    public static Brush? DesignAvatarBrush => GetBrush(DesignAvatarBrushKey);

    public static Brush? CodeAvatarBrush => GetBrush(CodeAvatarBrushKey);

    public static Brush? AnalyticsAvatarBrush => GetBrush(AnalyticsAvatarBrushKey);

    public static Brush? AvatarVariantDanishBrush => GetBrush(AvatarVariantDanishBrushKey);

    public static Brush? AvatarVariantEmilyBrush => GetBrush(AvatarVariantEmilyBrushKey);

    public static Brush? AvatarVariantFrankBrush => GetBrush(AvatarVariantFrankBrushKey);

    public static Brush? AccentBrush => GetBrush(AppAccentBrushKey);

    public static Brush? BadgeSurfaceBrush => GetBrush(AppBadgeSurfaceBrushKey);

    private static Brush? GetBrush(string resourceKey)
    {
        if (Application.Current?.Resources is { } resources &&
            resources.ContainsKey(resourceKey) &&
            resources[resourceKey] is Brush brush)
        {
            return brush;
        }

        return null;
    }
}
