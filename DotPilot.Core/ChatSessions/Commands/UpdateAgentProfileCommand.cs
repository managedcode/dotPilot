using DotPilot.Core.ControlPlaneDomain;
using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class UpdateAgentProfileCommand : Command<UpdateAgentProfileCommand.Payload>
{
    private readonly Payload _payload;

    public UpdateAgentProfileCommand(
        AgentProfileId agentId,
        string name,
        AgentProviderKind providerKind,
        string modelName,
        string systemPrompt,
        string description = "")
        : base(
            Guid.CreateVersion7(),
            nameof(UpdateAgentProfileCommand),
            new Payload(agentId, name, providerKind, modelName, systemPrompt, description))
    {
        _payload = new Payload(agentId, name, providerKind, modelName, systemPrompt, description);
        Value = _payload;
    }

    public AgentProfileId AgentId => _payload.AgentId;

    public string Name => _payload.Name;

    public AgentProviderKind ProviderKind => _payload.ProviderKind;

    public string ModelName => _payload.ModelName;

    public string SystemPrompt => _payload.SystemPrompt;

    public string Description => _payload.Description;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] AgentProfileId AgentId,
        [property: Id(1)] string Name,
        [property: Id(2)] AgentProviderKind ProviderKind,
        [property: Id(3)] string ModelName,
        [property: Id(4)] string SystemPrompt,
        [property: Id(5)] string Description);
}
