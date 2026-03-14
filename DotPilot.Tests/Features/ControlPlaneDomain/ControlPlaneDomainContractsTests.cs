using System.Text.Json;

namespace DotPilot.Tests.Features.ControlPlaneDomain;

public class ControlPlaneDomainContractsTests
{
    private const string SyntheticWorkspaceRootPath = "/repo/dotPilot";
    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 13, 10, 15, 30, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAt = new(2026, 3, 13, 10, 45, 30, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public void ControlPlaneIdentifiersProduceStableNonEmptyRepresentations()
    {
        IReadOnlyList<string> values =
        [
            WorkspaceId.New().ToString(),
            AgentProfileId.New().ToString(),
            SessionId.New().ToString(),
            FleetId.New().ToString(),
            PolicyId.New().ToString(),
            ProviderId.New().ToString(),
            ModelRuntimeId.New().ToString(),
            ToolCapabilityId.New().ToString(),
            ApprovalId.New().ToString(),
            ArtifactId.New().ToString(),
            TelemetryRecordId.New().ToString(),
            EvaluationId.New().ToString(),
        ];

        values.Should().OnlyContain(value => !string.IsNullOrWhiteSpace(value));
        values.Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void ControlPlaneContractsRoundTripThroughSystemTextJson()
    {
        var envelope = CreateEnvelope();

        var payload = JsonSerializer.Serialize(envelope, SerializerOptions);
        var roundTrip = JsonSerializer.Deserialize<ControlPlaneDomainEnvelope>(payload, SerializerOptions);

        roundTrip.Should().NotBeNull();
        roundTrip!.Should().BeEquivalentTo(envelope);
    }

    [Test]
    public void ControlPlaneContractsModelMixedProviderAndLocalRuntimeSessions()
    {
        var envelope = CreateEnvelope();

        envelope.Session.AgentProfileIds.Should().ContainInOrder(
            envelope.CodingAgent.Id,
            envelope.ReviewerAgent.Id);
        envelope.CodingAgent.ProviderId.Should().Be(envelope.Provider.Id);
        envelope.ReviewerAgent.ModelRuntimeId.Should().Be(envelope.LocalRuntime.Id);
        envelope.Fleet.ExecutionMode.Should().Be(FleetExecutionMode.Orchestrated);
        envelope.Policy.DefaultApprovalState.Should().Be(ApprovalState.Pending);
        envelope.Approval.Scope.Should().Be(ApprovalScope.CommandExecution);
        envelope.Artifact.Kind.Should().Be(ArtifactKind.Snapshot);
        envelope.Telemetry.Kind.Should().Be(TelemetrySignalKind.Trace);
        envelope.Evaluation.Metric.Should().Be(EvaluationMetricKind.ToolCallAccuracy);
    }

    private static ControlPlaneDomainEnvelope CreateEnvelope()
    {
        var tool = new ToolCapabilityDescriptor
        {
            Id = ToolCapabilityId.New(),
            Name = "workspace-edit",
            DisplayName = "Workspace Edit",
            Kind = ToolCapabilityKind.FileSystem,
            RequiresApproval = true,
            IsEnabledByDefault = true,
            Tags = ["write", "filesystem"],
        };

        var provider = new ProviderDescriptor
        {
            Id = ProviderId.New(),
            DisplayName = "Codex",
            CommandName = "codex",
            Status = ProviderConnectionStatus.Available,
            StatusSummary = "codex is available on PATH.",
            RequiresExternalToolchain = true,
            SupportedToolIds = [tool.Id],
        };

        var localRuntime = new ModelRuntimeDescriptor
        {
            Id = ModelRuntimeId.New(),
            DisplayName = "Local ONNX Runtime",
            EngineName = "ONNX Runtime",
            RuntimeKind = RuntimeKind.LocalModel,
            Status = ProviderConnectionStatus.Available,
            SupportedModelFamilies = ["phi", "qwen"],
        };

        var workspace = new WorkspaceDescriptor
        {
            Id = WorkspaceId.New(),
            Name = "dotPilot",
            RootPath = SyntheticWorkspaceRootPath,
            BranchName = "codex/issue-22-domain-model",
        };

        var codingAgent = new AgentProfileDescriptor
        {
            Id = AgentProfileId.New(),
            Name = "Implementation Agent",
            Role = AgentRoleKind.Coding,
            ProviderId = provider.Id,
            ToolCapabilityIds = [tool.Id],
            Tags = ["implementation", "provider"],
        };

        var reviewerAgent = new AgentProfileDescriptor
        {
            Id = AgentProfileId.New(),
            Name = "Runtime Reviewer",
            Role = AgentRoleKind.Reviewer,
            ModelRuntimeId = localRuntime.Id,
            ToolCapabilityIds = [tool.Id],
            Tags = ["review", "local"],
        };

        var fleet = new FleetDescriptor
        {
            Id = FleetId.New(),
            Name = "Mixed Provider Fleet",
            ExecutionMode = FleetExecutionMode.Orchestrated,
            AgentProfileIds = [codingAgent.Id, reviewerAgent.Id],
        };

        var session = new SessionDescriptor
        {
            Id = SessionId.New(),
            WorkspaceId = workspace.Id,
            Title = "Issue 22 domain slice rollout",
            Phase = SessionPhase.Execute,
            ApprovalState = ApprovalState.Pending,
            FleetId = fleet.Id,
            AgentProfileIds = [codingAgent.Id, reviewerAgent.Id],
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };

        var approval = new SessionApprovalRecord
        {
            Id = ApprovalId.New(),
            SessionId = session.Id,
            Scope = ApprovalScope.CommandExecution,
            State = ApprovalState.Pending,
            RequestedAction = "Run full solution tests",
            RequestedBy = codingAgent.Name,
            RequestedAt = UpdatedAt,
        };

        var artifact = new ArtifactDescriptor
        {
            Id = ArtifactId.New(),
            SessionId = session.Id,
            AgentProfileId = codingAgent.Id,
            Name = "runtime-foundation.snapshot.json",
            Kind = ArtifactKind.Snapshot,
            RelativePath = "artifacts/runtime-foundation.snapshot.json",
            CreatedAt = UpdatedAt,
        };

        var telemetry = new TelemetryRecord
        {
            Id = TelemetryRecordId.New(),
            SessionId = session.Id,
            Kind = TelemetrySignalKind.Trace,
            Name = "RuntimeFoundation.Execute",
            Summary = "Deterministic provider-independent trace",
            RecordedAt = UpdatedAt,
        };

        var evaluation = new EvaluationRecord
        {
            Id = EvaluationId.New(),
            SessionId = session.Id,
            ArtifactId = artifact.Id,
            Metric = EvaluationMetricKind.ToolCallAccuracy,
            Score = 0.98m,
            Outcome = EvaluationOutcome.Passed,
            Summary = "Tool calls matched the expected deterministic sequence.",
            EvaluatedAt = UpdatedAt,
        };

        return new ControlPlaneDomainEnvelope
        {
            Workspace = workspace,
            Tool = tool,
            Provider = provider,
            LocalRuntime = localRuntime,
            CodingAgent = codingAgent,
            ReviewerAgent = reviewerAgent,
            Fleet = fleet,
            Policy = new PolicyDescriptor
            {
                Id = PolicyId.New(),
                Name = "Desktop Local Policy",
                DefaultApprovalState = ApprovalState.Pending,
                AllowsNetworkAccess = false,
                AllowsFileSystemWrites = true,
                ProtectedScopes = [ApprovalScope.FileWrite, ApprovalScope.CommandExecution],
            },
            Session = session,
            Approval = approval,
            Artifact = artifact,
            Telemetry = telemetry,
            Evaluation = evaluation,
        };
    }

    private sealed record ControlPlaneDomainEnvelope
    {
        public WorkspaceDescriptor Workspace { get; init; } = new();

        public ToolCapabilityDescriptor Tool { get; init; } = new();

        public ProviderDescriptor Provider { get; init; } = new();

        public ModelRuntimeDescriptor LocalRuntime { get; init; } = new();

        public AgentProfileDescriptor CodingAgent { get; init; } = new();

        public AgentProfileDescriptor ReviewerAgent { get; init; } = new();

        public FleetDescriptor Fleet { get; init; } = new();

        public PolicyDescriptor Policy { get; init; } = new();

        public SessionDescriptor Session { get; init; } = new();

        public SessionApprovalRecord Approval { get; init; } = new();

        public ArtifactDescriptor Artifact { get; init; } = new();

        public TelemetryRecord Telemetry { get; init; } = new();

        public EvaluationRecord Evaluation { get; init; } = new();
    }
}
