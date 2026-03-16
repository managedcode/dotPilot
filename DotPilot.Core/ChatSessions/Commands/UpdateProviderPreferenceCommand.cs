using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class UpdateProviderPreferenceCommand : Command<UpdateProviderPreferenceCommand.Payload>
{
    private readonly Payload payload;

    public UpdateProviderPreferenceCommand(
        AgentProviderKind providerKind,
        bool isEnabled)
        : this(new Payload(providerKind, isEnabled))
    {
    }

    private UpdateProviderPreferenceCommand(Payload payload)
        : base(Guid.CreateVersion7(), nameof(UpdateProviderPreferenceCommand), payload)
    {
        this.payload = payload;
        Value = payload;
    }

    public AgentProviderKind ProviderKind => payload.ProviderKind;

    public bool IsEnabled => payload.IsEnabled;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] AgentProviderKind ProviderKind,
        [property: Id(1)] bool IsEnabled);
}
