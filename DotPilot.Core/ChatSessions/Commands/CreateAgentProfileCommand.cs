using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class CreateAgentProfileCommand : Command<CreateAgentProfileCommand.Payload>
{
    private readonly Payload _payload;

    public CreateAgentProfileCommand(
        string name,
        AgentProviderKind providerKind,
        string modelName,
        string systemPrompt)
        : base(
            Guid.CreateVersion7(),
            nameof(CreateAgentProfileCommand),
            new Payload(name, providerKind, modelName, systemPrompt))
    {
        _payload = new Payload(name, providerKind, modelName, systemPrompt);
        Value = _payload;
    }

    public string Name => _payload.Name;

    public AgentProviderKind ProviderKind => _payload.ProviderKind;

    public string ModelName => _payload.ModelName;

    public string SystemPrompt => _payload.SystemPrompt;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] string Name,
        [property: Id(1)] AgentProviderKind ProviderKind,
        [property: Id(2)] string ModelName,
        [property: Id(3)] string SystemPrompt);
}
