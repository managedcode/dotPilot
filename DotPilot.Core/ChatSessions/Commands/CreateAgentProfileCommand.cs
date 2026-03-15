using ManagedCode.Communication.Commands;
using DotPilot.Core.ControlPlaneDomain;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class CreateAgentProfileCommand : Command<CreateAgentProfileCommand.Payload>
{
    private readonly Payload _payload;

    public CreateAgentProfileCommand(
        string name,
        AgentRoleKind role,
        AgentProviderKind providerKind,
        string modelName,
        string systemPrompt,
        IReadOnlyList<string> capabilities)
        : base(
            Guid.CreateVersion7(),
            nameof(CreateAgentProfileCommand),
            new Payload(name, role, providerKind, modelName, systemPrompt, capabilities))
    {
        _payload = new Payload(name, role, providerKind, modelName, systemPrompt, capabilities);
        Value = _payload;
    }

    public string Name => _payload.Name;

    public AgentRoleKind Role => _payload.Role;

    public AgentProviderKind ProviderKind => _payload.ProviderKind;

    public string ModelName => _payload.ModelName;

    public string SystemPrompt => _payload.SystemPrompt;

    public IReadOnlyList<string> Capabilities => _payload.Capabilities;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] string Name,
        [property: Id(1)] AgentRoleKind Role,
        [property: Id(2)] AgentProviderKind ProviderKind,
        [property: Id(3)] string ModelName,
        [property: Id(4)] string SystemPrompt,
        [property: Id(5)] IReadOnlyList<string> Capabilities);
}
