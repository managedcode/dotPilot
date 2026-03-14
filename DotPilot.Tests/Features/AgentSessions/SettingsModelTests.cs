using DotPilot.Core.Features.AgentSessions;
using DotPilot.Presentation;
using DotPilot.Runtime.Features.AgentSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Features.AgentSessions;

public sealed class SettingsModelTests
{
    [Test]
    public async Task ToggleSelectedProviderUpdatesProjectionToEnabledDebugProvider()
    {
        await using var fixture = CreateFixture();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);

        var providers = await model.Providers;
        providers.Should().ContainSingle(provider => provider.Kind == AgentProviderKind.Debug);
        (await model.SelectedProviderTitle).Should().Be("Debug Provider");
        (await model.ToggleActionLabel).Should().Be("Enable provider");
        (await model.CanToggleSelectedProvider).Should().BeTrue();

        await model.ToggleSelectedProvider(CancellationToken.None);

        (await model.SelectedProviderTitle).Should().Be("Debug Provider");
        (await model.ToggleActionLabel).Should().Be("Disable provider");
        (await model.SelectedProvider).Should().NotBeNull();
        (await model.SelectedProvider)!.IsEnabled.Should().BeTrue();

        var workspace = await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None);
        workspace.Providers.Should().ContainSingle(provider =>
            provider.Kind == AgentProviderKind.Debug &&
            provider.IsEnabled &&
            provider.CanCreateAgents);
    }

    private static TestFixture CreateFixture()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        var workspaceState = provider.GetRequiredService<IAgentWorkspaceState>();
        return new TestFixture(provider, workspaceState);
    }

    private sealed class TestFixture(ServiceProvider provider, IAgentWorkspaceState workspaceState) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;

        public IAgentWorkspaceState WorkspaceState { get; } = workspaceState;

        public ValueTask DisposeAsync()
        {
            return Provider.DisposeAsync();
        }
    }
}
