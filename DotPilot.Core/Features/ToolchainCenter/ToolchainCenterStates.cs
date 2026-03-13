namespace DotPilot.Core.Features.ToolchainCenter;

public enum ToolchainReadinessState
{
    Missing,
    ActionRequired,
    Limited,
    Ready,
}

public enum ToolchainVersionStatus
{
    Missing,
    Unknown,
    Detected,
    UpdateAvailable,
}

public enum ToolchainAuthStatus
{
    Missing,
    Unknown,
    Connected,
}

public enum ToolchainHealthStatus
{
    Blocked,
    Warning,
    Healthy,
}

public enum ToolchainDiagnosticStatus
{
    Blocked,
    Failed,
    Warning,
    Ready,
    Passed,
}

public enum ToolchainConfigurationKind
{
    Secret,
    EnvironmentVariable,
    Setting,
}

public enum ToolchainConfigurationStatus
{
    Missing,
    Partial,
    Configured,
}

public enum ToolchainActionKind
{
    Install,
    Connect,
    Update,
    TestConnection,
    Troubleshoot,
    OpenDocs,
}

public enum ToolchainPollingStatus
{
    Idle,
    Healthy,
    Warning,
}
