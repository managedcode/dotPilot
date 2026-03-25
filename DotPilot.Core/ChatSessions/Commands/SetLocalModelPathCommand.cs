using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class SetLocalModelPathCommand : Command<SetLocalModelPathCommand.Payload>
{
    private readonly Payload payload;

    public SetLocalModelPathCommand(
        AgentProviderKind providerKind,
        string localModelPath)
        : this(new Payload(providerKind, localModelPath))
    {
    }

    private SetLocalModelPathCommand(Payload payload)
        : base(Guid.CreateVersion7(), nameof(SetLocalModelPathCommand), payload)
    {
        this.payload = payload;
        Value = payload;
    }

    public AgentProviderKind ProviderKind => payload.ProviderKind;

    public string LocalModelPath => payload.LocalModelPath;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] AgentProviderKind ProviderKind,
        [property: Id(1)] string LocalModelPath);
}
