using Microsoft.Extensions.Logging;

namespace DotPilot.Core.Workspace;

internal static partial class StartupWorkspaceHydrationLog
{
    [LoggerMessage(
        EventId = 1500,
        Level = LogLevel.Information,
        Message = "Starting startup workspace hydration.")]
    public static partial void HydrationStarted(ILogger logger);

    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Information,
        Message = "Completed startup workspace hydration.")]
    public static partial void HydrationCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 1502,
        Level = LogLevel.Error,
        Message = "Startup workspace hydration failed.")]
    public static partial void HydrationFailed(ILogger logger, Exception exception);
}

internal static partial class StartupWorkspaceHydrationHostedServiceLog
{
    [LoggerMessage(
        EventId = 1503,
        Level = LogLevel.Error,
        Message = "Startup workspace hydration background task failed.")]
    public static partial void HydrationStartFailed(ILogger logger, Exception exception);
}
