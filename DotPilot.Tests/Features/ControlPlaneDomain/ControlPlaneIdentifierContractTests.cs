namespace DotPilot.Tests.Features.ControlPlaneDomain;

public sealed class ControlPlaneIdentifierContractTests
{
    [Test]
    public void NewlyCreatedIdentifiersUseVersionSevenTokens()
    {
        IReadOnlyList<string> identifiers =
        [
            WorkspaceId.New().ToString(),
            AgentProfileId.New().ToString(),
            SessionId.New().ToString(),
            FleetId.New().ToString(),
            ProviderId.New().ToString(),
            ModelRuntimeId.New().ToString(),
            ToolCapabilityId.New().ToString(),
            ApprovalId.New().ToString(),
            ArtifactId.New().ToString(),
            TelemetryRecordId.New().ToString(),
            EvaluationId.New().ToString(),
        ];

        identifiers.Should().OnlyContain(identifier => identifier.Length == 32);
        identifiers.Should().OnlyContain(identifier => identifier[12] == '7');
    }

    [Test]
    public void DefaultDescriptorsExposeSerializationSafeDefaults()
    {
        var workspace = new WorkspaceDescriptor();
        var agent = new AgentProfileDescriptor();
        var fleet = new FleetDescriptor();
        var tool = new ToolCapabilityDescriptor();
        var provider = new ProviderDescriptor();
        var runtime = new ModelRuntimeDescriptor();
        var session = new SessionDescriptor();
        var approval = new SessionApprovalRecord();
        var artifact = new ArtifactDescriptor();
        var telemetry = new TelemetryRecord();
        var evaluation = new EvaluationRecord();

        workspace.Name.Should().BeEmpty();
        workspace.RootPath.Should().BeEmpty();
        workspace.BranchName.Should().BeEmpty();
        agent.Name.Should().BeEmpty();
        agent.ToolCapabilityIds.Should().BeEmpty();
        agent.Tags.Should().BeEmpty();
        fleet.Name.Should().BeEmpty();
        fleet.AgentProfileIds.Should().BeEmpty();
        tool.Name.Should().BeEmpty();
        tool.DisplayName.Should().BeEmpty();
        tool.Tags.Should().BeEmpty();
        provider.DisplayName.Should().BeEmpty();
        provider.CommandName.Should().BeEmpty();
        provider.StatusSummary.Should().BeEmpty();
        provider.Status.Should().Be(ProviderConnectionStatus.Unavailable);
        provider.SupportedToolIds.Should().BeEmpty();
        runtime.DisplayName.Should().BeEmpty();
        runtime.EngineName.Should().BeEmpty();
        runtime.Status.Should().Be(ProviderConnectionStatus.Unavailable);
        runtime.SupportedModelFamilies.Should().BeEmpty();
        session.Title.Should().BeEmpty();
        session.Phase.Should().Be(SessionPhase.Plan);
        session.ApprovalState.Should().Be(ApprovalState.NotRequired);
        session.AgentProfileIds.Should().BeEmpty();
        approval.State.Should().Be(ApprovalState.Pending);
        approval.RequestedAction.Should().BeEmpty();
        approval.RequestedBy.Should().BeEmpty();
        artifact.Name.Should().BeEmpty();
        artifact.RelativePath.Should().BeEmpty();
        telemetry.Name.Should().BeEmpty();
        telemetry.Summary.Should().BeEmpty();
        evaluation.Outcome.Should().Be(EvaluationOutcome.NeedsReview);
        evaluation.Summary.Should().BeEmpty();
    }
}
