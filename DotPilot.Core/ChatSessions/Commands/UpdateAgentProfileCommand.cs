using DotPilot.Core.ControlPlaneDomain;
using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class UpdateAgentProfileCommand : Command<UpdateAgentProfileCommand.Payload>
{
    private readonly Payload payload;

    public UpdateAgentProfileCommand(
        AgentProfileId agentId,
        string name,
        AgentProviderKind providerKind,
        string modelName,
        string systemPrompt,
        string description = "")
        : this(new Payload(agentId, name, providerKind, modelName, systemPrompt, description))
    {
    }

    private UpdateAgentProfileCommand(Payload payload)
        : base(Guid.CreateVersion7(), nameof(UpdateAgentProfileCommand), payload)
    {
        this.payload = payload;
        Value = payload;
    }

    public AgentProfileId AgentId => payload.AgentId;

    public string Name => payload.Name;

    public AgentProviderKind ProviderKind => payload.ProviderKind;

    public string ModelName => payload.ModelName;

    public string SystemPrompt => payload.SystemPrompt;

    public string Description => payload.Description;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] AgentProfileId AgentId,
        [property: Id(1)] string Name,
        [property: Id(2)] AgentProviderKind ProviderKind,
        [property: Id(3)] string ModelName,
        [property: Id(4)] string SystemPrompt,
        [property: Id(5)] string Description);
}
