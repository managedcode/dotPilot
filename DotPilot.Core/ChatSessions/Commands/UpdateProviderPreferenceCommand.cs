using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class UpdateProviderPreferenceCommand : Command<UpdateProviderPreferenceCommand.Payload>
{
    private readonly Payload _payload;

    public UpdateProviderPreferenceCommand(
        AgentProviderKind providerKind,
        bool isEnabled)
        : base(
            Guid.CreateVersion7(),
            nameof(UpdateProviderPreferenceCommand),
            new Payload(providerKind, isEnabled))
    {
        _payload = new Payload(providerKind, isEnabled);
        Value = _payload;
    }

    public AgentProviderKind ProviderKind => _payload.ProviderKind;

    public bool IsEnabled => _payload.IsEnabled;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] AgentProviderKind ProviderKind,
        [property: Id(1)] bool IsEnabled);
}
