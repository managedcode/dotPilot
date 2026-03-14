using DotPilot.Core.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

internal static class EmbeddedRuntimeTrafficPolicy
{
    private const string PolicySummary =
        "Client and grain transitions stay explicit so the embedded host can reject unsupported hops before the runtime model grows.";
    private const string MermaidHeader = "flowchart LR";
    private const string MermaidArrow = " --> ";
    private const string MermaidActiveArrow = " ==> ";
    private const string ClientTargetMethods = "GetAsync, UpsertAsync";

    public static string Summary => PolicySummary;

    public static IReadOnlyList<EmbeddedRuntimeTrafficTransitionDescriptor> AllowedTransitions =>
    [
        CreateClientTransition(EmbeddedRuntimeHostNames.SessionGrainName),
        CreateClientTransition(EmbeddedRuntimeHostNames.WorkspaceGrainName),
        CreateClientTransition(EmbeddedRuntimeHostNames.FleetGrainName),
        CreateClientTransition(EmbeddedRuntimeHostNames.PolicyGrainName),
        CreateClientTransition(EmbeddedRuntimeHostNames.ArtifactGrainName),
        CreateTransition(EmbeddedRuntimeHostNames.SessionGrainName, EmbeddedRuntimeHostNames.WorkspaceGrainName, nameof(ISessionGrain.UpsertAsync), nameof(IWorkspaceGrain.GetAsync)),
        CreateTransition(EmbeddedRuntimeHostNames.SessionGrainName, EmbeddedRuntimeHostNames.FleetGrainName, nameof(ISessionGrain.UpsertAsync), nameof(IFleetGrain.GetAsync)),
        CreateTransition(EmbeddedRuntimeHostNames.SessionGrainName, EmbeddedRuntimeHostNames.PolicyGrainName, nameof(ISessionGrain.UpsertAsync), nameof(IPolicyGrain.GetAsync)),
        CreateTransition(EmbeddedRuntimeHostNames.SessionGrainName, EmbeddedRuntimeHostNames.ArtifactGrainName, nameof(ISessionGrain.UpsertAsync), nameof(IArtifactGrain.UpsertAsync)),
        CreateTransition(EmbeddedRuntimeHostNames.FleetGrainName, EmbeddedRuntimeHostNames.PolicyGrainName, nameof(IFleetGrain.GetAsync), nameof(IPolicyGrain.GetAsync)),
    ];

    public static bool IsAllowed(EmbeddedRuntimeTrafficProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var sourceName = GetGrainName(probe.SourceGrainType);
        var targetName = GetGrainName(probe.TargetGrainType);

        return AllowedTransitions.Any(transition =>
            string.Equals(transition.Source, sourceName, StringComparison.Ordinal) &&
            string.Equals(transition.Target, targetName, StringComparison.Ordinal) &&
            transition.SourceMethods.Contains(probe.SourceMethod, StringComparer.Ordinal) &&
            transition.TargetMethods.Contains(probe.TargetMethod, StringComparer.Ordinal));
    }

    public static string CreateMermaidDiagram()
    {
        return CreateMermaidDiagramCore(activeTransition: null);
    }

    public static string CreateMermaidDiagram(EmbeddedRuntimeTrafficProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var activeTransition = (
            Source: GetGrainName(probe.SourceGrainType),
            Target: GetGrainName(probe.TargetGrainType),
            SourceMethod: probe.SourceMethod,
            TargetMethod: probe.TargetMethod);
        return CreateMermaidDiagramCore(activeTransition);
    }

    private static EmbeddedRuntimeTrafficTransitionDescriptor CreateClientTransition(string target)
    {
        return new(
            EmbeddedRuntimeHostNames.ClientSourceName,
            target,
            [EmbeddedRuntimeHostNames.ClientSourceMethodName],
            [nameof(ISessionGrain.GetAsync), nameof(ISessionGrain.UpsertAsync)],
            false);
    }

    private static EmbeddedRuntimeTrafficTransitionDescriptor CreateTransition(
        string source,
        string target,
        string sourceMethod,
        string targetMethod)
    {
        return new(
            source,
            target,
            [sourceMethod],
            [targetMethod],
            false);
    }

    private static string GetGrainName(Type grainType)
    {
        ArgumentNullException.ThrowIfNull(grainType);

        return grainType == typeof(ISessionGrain) ? EmbeddedRuntimeHostNames.SessionGrainName
            : grainType == typeof(IWorkspaceGrain) ? EmbeddedRuntimeHostNames.WorkspaceGrainName
            : grainType == typeof(IFleetGrain) ? EmbeddedRuntimeHostNames.FleetGrainName
            : grainType == typeof(IPolicyGrain) ? EmbeddedRuntimeHostNames.PolicyGrainName
            : grainType == typeof(IArtifactGrain) ? EmbeddedRuntimeHostNames.ArtifactGrainName
            : grainType.Name;
    }

    private static string CreateMermaidDiagramCore((string Source, string Target, string SourceMethod, string TargetMethod)? activeTransition)
    {
        var lines = new List<string>(AllowedTransitions.Count + 1)
        {
            MermaidHeader,
        };

        foreach (var transition in AllowedTransitions)
        {
            var isActive = activeTransition is not null &&
                string.Equals(transition.Source, activeTransition.Value.Source, StringComparison.Ordinal) &&
                string.Equals(transition.Target, activeTransition.Value.Target, StringComparison.Ordinal) &&
                transition.SourceMethods.Contains(activeTransition.Value.SourceMethod, StringComparer.Ordinal) &&
                transition.TargetMethods.Contains(activeTransition.Value.TargetMethod, StringComparer.Ordinal);
            var arrow = isActive ? MermaidActiveArrow : MermaidArrow;
            var targetMethods = transition.Target == EmbeddedRuntimeHostNames.ClientSourceName
                ? ClientTargetMethods
                : string.Join(", ", transition.TargetMethods);
            lines.Add(
                string.Concat(
                    transition.Source,
                    arrow,
                    transition.Target,
                    " : ",
                    string.Join(", ", transition.SourceMethods),
                    " -> ",
                    targetMethods));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
