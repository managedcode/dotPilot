namespace DotPilot.Core;

[GenerateSerializer]
public sealed record PolicyDescriptor
{
    [Id(0)]
    public PolicyId Id { get; init; }

    [Id(1)]
    public string Name { get; init; } = string.Empty;

    [Id(2)]
    public ApprovalState DefaultApprovalState { get; init; } = ApprovalState.NotRequired;

    [Id(3)]
    public bool AllowsNetworkAccess { get; init; }

    [Id(4)]
    public bool AllowsFileSystemWrites { get; init; }

    [Id(5)]
    public ApprovalScope[] ProtectedScopes { get; init; } = [];
}
