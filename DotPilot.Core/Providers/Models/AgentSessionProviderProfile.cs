
namespace DotPilot.Core.Providers;

internal sealed record AgentSessionProviderProfile(
    AgentProviderKind Kind,
    string DisplayName,
    string CommandName,
    string DefaultModelName,
    IReadOnlyList<string> SupportedModelNames,
    string InstallCommand,
    bool IsBuiltIn,
    bool SupportsLiveExecution);
