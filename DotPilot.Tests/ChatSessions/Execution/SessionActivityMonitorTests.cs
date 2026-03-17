using DotPilot.Core;
using DotPilot.Core.ChatSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.ChatSessions.Execution;

public sealed class SessionActivityMonitorTests
{
    [Test]
    public void BeginActivityOrdersActiveSessionsByLatestDistinctLeaseAndUsesTheMostRecentLeaseForTheSnapshot()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<ISessionActivityMonitor>();
        var firstSessionId = SessionId.New();
        var secondSessionId = SessionId.New();
        var firstAgentId = AgentProfileId.New();
        var secondAgentId = AgentProfileId.New();

        using var firstLease = monitor.BeginActivity(
            new SessionActivityDescriptor(
                firstSessionId,
                "First session",
                firstAgentId,
                "First agent",
                "Codex"));
        using var secondLease = monitor.BeginActivity(
            new SessionActivityDescriptor(
                secondSessionId,
                "Second session",
                secondAgentId,
                "Second agent",
                "Claude Code"));
        using var refreshedFirstLease = monitor.BeginActivity(
            new SessionActivityDescriptor(
                firstSessionId,
                "First session",
                firstAgentId,
                "First agent (latest)",
                "GitHub Copilot"));

        var snapshot = monitor.Current;

        snapshot.HasActiveSessions.Should().BeTrue();
        snapshot.ActiveSessionCount.Should().Be(2);
        snapshot.ActiveSessions.Select(session => session.SessionId)
            .Should()
            .Equal(secondSessionId, firstSessionId);
        snapshot.SessionId.Should().Be(firstSessionId);
        snapshot.AgentProfileId.Should().Be(firstAgentId);
        snapshot.AgentName.Should().Be("First agent (latest)");
        snapshot.ProviderDisplayName.Should().Be("GitHub Copilot");
    }
}
