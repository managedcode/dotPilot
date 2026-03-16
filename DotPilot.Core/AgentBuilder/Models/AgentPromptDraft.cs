
namespace DotPilot.Core.AgentBuilder;

public sealed record AgentPromptDraft(
    string Prompt,
    string Name,
    string Description,
    AgentProviderKind ProviderKind,
    string ModelName,
    string SystemPrompt);
