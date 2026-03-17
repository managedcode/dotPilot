using DotPilot.Core.ChatSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.ChatSessions;

[NonParallelizable]
public sealed class RealProviderSessionSmokeTests
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(250);
    private const int DeleteRetryCount = 20;
    private const string HelloMessage = "hello";
    private static readonly string[] EnvironmentIssueMarkers =
    [
        "auth",
        "authenticate",
        "authentication",
        "login",
        "log in",
        "sign in",
        "not logged",
        "not signed",
        "permission denied",
        "unauthorized",
        "forbidden",
        "token",
        "api key",
        "rate limit",
        "quota",
        "subscription",
        "billing",
    ];

    [TestCase(AgentProviderKind.Codex, "Codex")]
    [TestCase(AgentProviderKind.ClaudeCode, "Claude Code")]
    [TestCase(AgentProviderKind.GitHubCopilot, "GitHub Copilot")]
    public async Task CreateAgentAndSendHelloWorksForRealProviderWhenRuntimeIsAvailable(
        AgentProviderKind providerKind,
        string providerDisplayName)
    {
        await using var fixture = CreateFixture();

        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(providerKind, true),
            CancellationToken.None)).ShouldSucceed();
        if (!provider.CanCreateAgents)
        {
            Assert.Ignore($"{providerDisplayName} is not creatable in this environment: {provider.StatusSummary}");
        }

        var agent = (await fixture.Service.CreateAgentAsync(
            new CreateAgentProfileCommand(
                $"{providerDisplayName} Smoke Agent",
                providerKind,
                provider.SuggestedModelName,
                "Reply briefly and clearly to a hello message.",
                $"{providerDisplayName} smoke-verification agent."),
            CancellationToken.None)).ShouldSucceed();
        var session = (await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand($"Session with {providerDisplayName} Smoke Agent", agent.Id),
            CancellationToken.None)).ShouldSucceed();

        using var cancellationSource = new CancellationTokenSource(SendTimeout);
        List<SessionStreamEntry> streamedEntries = [];
        string? runtimeFailure = null;
        try
        {
            await foreach (var result in fixture.Service.SendMessageAsync(
                               new SendSessionMessageCommand(session.Session.Id, HelloMessage),
                               cancellationSource.Token))
            {
                if (!result.IsSuccess)
                {
                    runtimeFailure = result.ToDisplayMessage("Live provider execution failed.");
                    break;
                }

                streamedEntries.Add(result.Value!);
            }
        }
        catch (OperationCanceledException)
        {
            Assert.Ignore($"{providerDisplayName} did not complete a hello reply within {SendTimeout.TotalSeconds:0} seconds.");
        }

        IgnoreWhenEnvironmentIsNotReady(providerDisplayName, runtimeFailure);

        var errorText = string.Join(
            Environment.NewLine,
            streamedEntries
                .Where(static entry => entry.Kind == SessionStreamEntryKind.Error)
                .Select(static entry => entry.Text));
        IgnoreWhenEnvironmentIsNotReady(providerDisplayName, errorText);

        runtimeFailure.Should().BeNullOrWhiteSpace();
        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.UserMessage);
        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.ToolStarted);
        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.ToolCompleted);
        streamedEntries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.Error);
        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.AssistantMessage &&
            !string.IsNullOrWhiteSpace(entry.Text));
    }

    private static void IgnoreWhenEnvironmentIsNotReady(string providerDisplayName, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!EnvironmentIssueMarkers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Assert.Ignore($"{providerDisplayName} live execution is unavailable in this environment: {message}");
    }

    private static TestFixture CreateFixture()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "DotPilot.Tests",
            nameof(RealProviderSessionSmokeTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
            RuntimeSessionDirectoryPath = Path.Combine(tempRoot, "runtime-sessions"),
            ChatHistoryDirectoryPath = Path.Combine(tempRoot, "chat-history"),
            PlaygroundDirectoryPath = Path.Combine(tempRoot, "playground"),
        });

        var provider = services.BuildServiceProvider();
        return new TestFixture(
            provider,
            provider.GetRequiredService<IAgentSessionService>(),
            tempRoot);
    }

    private sealed class TestFixture(
        ServiceProvider provider,
        IAgentSessionService service,
        string tempRoot) : IAsyncDisposable
    {
        public IAgentSessionService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            DeleteDirectoryWithRetry(tempRoot);
        }
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        for (var attempt = 0; attempt < DeleteRetryCount; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < DeleteRetryCount - 1)
            {
                Thread.Sleep(DeleteRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < DeleteRetryCount - 1)
            {
                Thread.Sleep(DeleteRetryDelay);
            }
        }
    }
}
