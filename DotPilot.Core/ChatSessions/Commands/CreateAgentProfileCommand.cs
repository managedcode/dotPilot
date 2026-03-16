using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class CreateAgentProfileCommand : Command<CreateAgentProfileCommand.Payload>
{
    private readonly Payload payload;

    public CreateAgentProfileCommand(
        string name,
        AgentProviderKind providerKind,
        string modelName,
        string systemPrompt,
        string description = "")
        : this(new Payload(name, providerKind, modelName, systemPrompt, description))
    {
    }

    private CreateAgentProfileCommand(Payload payload)
        : base(Guid.CreateVersion7(), nameof(CreateAgentProfileCommand), payload)
    {
        this.payload = payload;
        Value = payload;
    }

    public string Name => payload.Name;

    public AgentProviderKind ProviderKind => payload.ProviderKind;

    public string ModelName => payload.ModelName;

    public string SystemPrompt => payload.SystemPrompt;

    public string Description => payload.Description;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] string Name,
        [property: Id(1)] AgentProviderKind ProviderKind,
        [property: Id(2)] string ModelName,
        [property: Id(3)] string SystemPrompt,
        [property: Id(4)] string Description);
}
