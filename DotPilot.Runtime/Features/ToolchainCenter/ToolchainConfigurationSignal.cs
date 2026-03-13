using DotPilot.Core.Features.ToolchainCenter;

namespace DotPilot.Runtime.Features.ToolchainCenter;

internal sealed record ToolchainConfigurationSignal(
    string Name,
    string Summary,
    ToolchainConfigurationKind Kind,
    bool IsSensitive,
    bool IsRequiredForReadiness);
