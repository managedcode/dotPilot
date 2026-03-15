namespace DotPilot.Core.ControlPlaneDomain;

public enum SessionPhase
{
    Plan,
    Execute,
    Review,
    Paused,
    Completed,
    Failed,
}

public enum ProviderConnectionStatus
{
    Available,
    Unavailable,
    RequiresAuthentication,
    Misconfigured,
    Outdated,
}

public enum ApprovalState
{
    NotRequired,
    Pending,
    Approved,
    Rejected,
}

public enum AgentRoleKind
{
    Coding,
    Research,
    Analyst,
    Reviewer,
    Operator,
    Orchestrator,
}

public enum FleetExecutionMode
{
    SingleAgent,
    Parallel,
    Orchestrated,
}

public enum RuntimeKind
{
    Provider,
    LocalModel,
}

public enum ToolCapabilityKind
{
    Command,
    FileSystem,
    Git,
    Mcp,
    Diagnostics,
}

public enum ApprovalScope
{
    FileWrite,
    CommandExecution,
    ToolCall,
    NetworkAccess,
    SessionResume,
}

public enum ArtifactKind
{
    Plan,
    Snapshot,
    Diff,
    Log,
    Screenshot,
    Transcript,
    Report,
}

public enum TelemetrySignalKind
{
    Trace,
    Metric,
    Log,
    Event,
}

public enum EvaluationMetricKind
{
    Relevance,
    Groundedness,
    Completeness,
    TaskAdherence,
    ToolCallAccuracy,
    Safety,
}

public enum EvaluationOutcome
{
    Passed,
    NeedsReview,
    Failed,
}
