namespace DotPilot.Core.Features.ControlPlaneDomain;

public sealed record ToolCapabilityDescriptor
{
    public ToolCapabilityId Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public ToolCapabilityKind Kind { get; init; }

    public bool RequiresApproval { get; init; }

    public bool IsEnabledByDefault { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}

public sealed record ProviderDescriptor
{
    public ProviderId Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string CommandName { get; init; } = string.Empty;

    public ProviderConnectionStatus Status { get; init; } = ProviderConnectionStatus.Unavailable;

    public string StatusSummary { get; init; } = string.Empty;

    public bool RequiresExternalToolchain { get; init; }

    public IReadOnlyList<ToolCapabilityId> SupportedToolIds { get; init; } = [];
}

public sealed record ModelRuntimeDescriptor
{
    public ModelRuntimeId Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string EngineName { get; init; } = string.Empty;

    public RuntimeKind RuntimeKind { get; init; }

    public ProviderConnectionStatus Status { get; init; } = ProviderConnectionStatus.Unavailable;

    public IReadOnlyList<string> SupportedModelFamilies { get; init; } = [];
}
