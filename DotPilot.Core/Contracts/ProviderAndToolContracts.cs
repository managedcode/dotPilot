namespace DotPilot.Core;

[GenerateSerializer]
public sealed record ToolCapabilityDescriptor
{
    [Id(0)]
    public ToolCapabilityId Id { get; init; }

    [Id(1)]
    public string Name { get; init; } = string.Empty;

    [Id(2)]
    public string DisplayName { get; init; } = string.Empty;

    [Id(3)]
    public ToolCapabilityKind Kind { get; init; }

    [Id(4)]
    public bool RequiresApproval { get; init; }

    [Id(5)]
    public bool IsEnabledByDefault { get; init; }

    [Id(6)]
    public string[] Tags { get; init; } = [];
}

[GenerateSerializer]
public sealed record ProviderDescriptor
{
    [Id(0)]
    public ProviderId Id { get; init; }

    [Id(1)]
    public string DisplayName { get; init; } = string.Empty;

    [Id(2)]
    public string CommandName { get; init; } = string.Empty;

    [Id(3)]
    public ProviderConnectionStatus Status { get; init; } = ProviderConnectionStatus.Unavailable;

    [Id(4)]
    public string StatusSummary { get; init; } = string.Empty;

    [Id(5)]
    public bool RequiresExternalToolchain { get; init; }

    [Id(6)]
    public ToolCapabilityId[] SupportedToolIds { get; init; } = [];
}

[GenerateSerializer]
public sealed record ModelRuntimeDescriptor
{
    [Id(0)]
    public ModelRuntimeId Id { get; init; }

    [Id(1)]
    public string DisplayName { get; init; } = string.Empty;

    [Id(2)]
    public string EngineName { get; init; } = string.Empty;

    [Id(3)]
    public RuntimeKind RuntimeKind { get; init; }

    [Id(4)]
    public ProviderConnectionStatus Status { get; init; } = ProviderConnectionStatus.Unavailable;

    [Id(5)]
    public string[] SupportedModelFamilies { get; init; } = [];
}
