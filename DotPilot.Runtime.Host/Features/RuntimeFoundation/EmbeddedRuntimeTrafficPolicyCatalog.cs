using DotPilot.Core.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

internal sealed class EmbeddedRuntimeTrafficPolicyCatalog : IEmbeddedRuntimeTrafficPolicyCatalog
{
    public EmbeddedRuntimeTrafficPolicySnapshot GetSnapshot()
    {
        return new(
            RuntimeFoundationIssues.GrainTrafficPolicy,
            RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.GrainTrafficPolicy),
            EmbeddedRuntimeTrafficPolicy.Summary,
            EmbeddedRuntimeTrafficPolicy.CreateMermaidDiagram(),
            EmbeddedRuntimeTrafficPolicy.AllowedTransitions);
    }

    public EmbeddedRuntimeTrafficDecision Evaluate(EmbeddedRuntimeTrafficProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        return new EmbeddedRuntimeTrafficDecision(
            EmbeddedRuntimeTrafficPolicy.IsAllowed(probe),
            EmbeddedRuntimeTrafficPolicy.CreateMermaidDiagram(probe));
    }
}
