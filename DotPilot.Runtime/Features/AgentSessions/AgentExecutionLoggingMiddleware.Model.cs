using System.Globalization;
using DotPilot.Core.Features.AgentSessions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DotPilot.Runtime.Features.AgentSessions;

internal sealed partial class AgentExecutionLoggingMiddleware
{
    private const string SessionIdPropertyName = "dotpilot.session.id";
    private const string RunIdPropertyName = "dotpilot.run.id";
    private const string AgentIdPropertyName = "dotpilot.agent.id";
    private const string AgentNamePropertyName = "dotpilot.agent.name";
    private const string ProviderKindPropertyName = "dotpilot.provider.kind";
    private const string ProviderDisplayNamePropertyName = "dotpilot.provider.display_name";
    private const string ModelNamePropertyName = "dotpilot.model.name";
    private const string UnknownValue = "<unknown>";
    private const string NoneValue = "<none>";

    private static IReadOnlyList<ChatMessage> MaterializeMessages(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return messages as IReadOnlyList<ChatMessage> ?? messages.ToArray();
    }

    private static int CountMessageCharacters(IEnumerable<ChatMessage> messages)
    {
        var total = 0;
        foreach (var message in messages)
        {
            total += message.Text?.Length ?? 0;
        }

        return total;
    }

    private static int CountTools(ChatOptions? options)
    {
        return options?.Tools?.Count ?? 0;
    }

    private static AdditionalPropertiesDictionary CreateAdditionalProperties(AgentRunLogContext runContext)
    {
        return new AdditionalPropertiesDictionary
        {
            [RunIdPropertyName] = runContext.RunId,
            [SessionIdPropertyName] = runContext.SessionId,
            [AgentIdPropertyName] = runContext.AgentId.ToString("N", CultureInfo.InvariantCulture),
            [AgentNamePropertyName] = runContext.AgentName,
            [ProviderKindPropertyName] = runContext.ProviderKind.ToString(),
            [ProviderDisplayNamePropertyName] = runContext.ProviderDisplayName,
            [ModelNamePropertyName] = runContext.ModelName,
        };
    }

    private static AgentRunLogContext ResolveRunContext(
        AgentExecutionDescriptor descriptor,
        AgentRunOptions? options)
    {
        var properties = options?.AdditionalProperties;
        return new AgentRunLogContext(
            GetAdditionalProperty(properties, RunIdPropertyName, NoneValue),
            GetAdditionalProperty(properties, SessionIdPropertyName, UnknownValue),
            descriptor.AgentId,
            GetAdditionalProperty(properties, AgentNamePropertyName, descriptor.AgentName),
            descriptor.ProviderKind,
            GetAdditionalProperty(properties, ProviderDisplayNamePropertyName, descriptor.ProviderDisplayName),
            GetAdditionalProperty(properties, ModelNamePropertyName, descriptor.ModelName));
    }

    private IDisposable? BeginScope(AgentRunLogContext runContext)
    {
        return logger.BeginScope(new Dictionary<string, object?>
        {
            [RunIdPropertyName] = runContext.RunId,
            [SessionIdPropertyName] = runContext.SessionId,
            [AgentIdPropertyName] = runContext.AgentId.ToString("N", CultureInfo.InvariantCulture),
            [AgentNamePropertyName] = runContext.AgentName,
            [ProviderKindPropertyName] = runContext.ProviderKind.ToString(),
            [ProviderDisplayNamePropertyName] = runContext.ProviderDisplayName,
            [ModelNamePropertyName] = runContext.ModelName,
        });
    }

    private static string GetAdditionalProperty(
        AdditionalPropertiesDictionary? properties,
        string key,
        string fallback)
    {
        if (properties is not null &&
            properties.TryGetValue(key, out var value) &&
            value is not null)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
        }

        return fallback;
    }
}

internal sealed record AgentExecutionDescriptor(
    Guid AgentId,
    string AgentName,
    AgentProviderKind ProviderKind,
    string ProviderDisplayName,
    string ModelName);

internal sealed record AgentRunLogContext(
    string RunId,
    string SessionId,
    Guid AgentId,
    string AgentName,
    AgentProviderKind ProviderKind,
    string ProviderDisplayName,
    string ModelName);

internal sealed record AgentExecutionRunConfiguration(
    AgentRunLogContext Context,
    ChatClientAgentRunOptions Options);
