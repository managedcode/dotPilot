namespace DotPilot.Core.Features.RuntimeFoundation;

public sealed record EmbeddedRuntimeTrafficTransitionDescriptor(
    string Source,
    string Target,
    IReadOnlyList<string> SourceMethods,
    IReadOnlyList<string> TargetMethods,
    bool IsReentrant);

public sealed record EmbeddedRuntimeTrafficPolicySnapshot(
    int IssueNumber,
    string IssueLabel,
    string Summary,
    string MermaidDiagram,
    IReadOnlyList<EmbeddedRuntimeTrafficTransitionDescriptor> AllowedTransitions);

public sealed record EmbeddedRuntimeTrafficProbe(
    Type SourceGrainType,
    string SourceMethod,
    Type TargetGrainType,
    string TargetMethod);

public sealed record EmbeddedRuntimeTrafficDecision(
    bool IsAllowed,
    string MermaidDiagram);

public interface IEmbeddedRuntimeTrafficPolicyCatalog
{
    EmbeddedRuntimeTrafficPolicySnapshot GetSnapshot();

    EmbeddedRuntimeTrafficDecision Evaluate(EmbeddedRuntimeTrafficProbe probe);
}
