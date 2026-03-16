using Microsoft.Agents.AI;

namespace DotPilot.Core.ChatSessions;

internal sealed record RuntimeConversationContext(
    AIAgent Agent,
    AgentSession Session,
    AgentExecutionDescriptor Descriptor,
    bool IsTransient = false);
