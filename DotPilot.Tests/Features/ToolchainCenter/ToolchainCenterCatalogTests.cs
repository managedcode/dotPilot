namespace DotPilot.Tests.Features.ToolchainCenter;

public class ToolchainCenterCatalogTests
{
    [Test]
    public void CatalogIncludesEpicIssueCoverageAndAllExternalProviders()
    {
        using var catalog = CreateCatalog();

        var snapshot = catalog.GetSnapshot();
        var coveredIssues = snapshot.Workstreams
            .Select(workstream => workstream.IssueNumber)
            .Concat(snapshot.Providers.Select(provider => provider.IssueNumber))
            .Order()
            .ToArray();

        snapshot.EpicLabel.Should().Be(ToolchainCenterIssues.FormatIssueLabel(ToolchainCenterIssues.ToolchainCenterEpic));
        coveredIssues.Should().Equal(
            ToolchainCenterIssues.ToolchainCenterUi,
            ToolchainCenterIssues.CodexReadiness,
            ToolchainCenterIssues.ClaudeCodeReadiness,
            ToolchainCenterIssues.GitHubCopilotReadiness,
            ToolchainCenterIssues.ConnectionDiagnostics,
            ToolchainCenterIssues.ProviderConfiguration,
            ToolchainCenterIssues.BackgroundPolling);
        snapshot.Providers.Select(provider => provider.Provider.CommandName).Should().ContainInOrder("codex", "claude", "gh");
    }

    [Test]
    public void CatalogSurfacesDiagnosticsConfigurationAndPollingForEachProvider()
    {
        using var catalog = CreateCatalog();

        var snapshot = catalog.GetSnapshot();

        snapshot.BackgroundPolling.RefreshInterval.Should().Be(TimeSpan.FromMinutes(5));
        snapshot.Providers.Should().OnlyContain(provider =>
            provider.Diagnostics.Any(diagnostic => diagnostic.Name == "Launch") &&
            provider.Diagnostics.Any(diagnostic => diagnostic.Name == "Connection test") &&
            provider.Diagnostics.Any(diagnostic => diagnostic.Name == "Resume test") &&
            provider.Configuration.Any(entry => entry.Kind == ToolchainConfigurationKind.Secret) &&
            provider.Configuration.Any(entry => entry.Name == $"{provider.Provider.CommandName} path") &&
            provider.Polling.RefreshInterval == TimeSpan.FromMinutes(5));
    }

    [Test]
    public void CatalogCanStartAndDisposeBackgroundPolling()
    {
        using var catalog = new ToolchainCenterCatalog(TimeProvider.System, startBackgroundPolling: true);

        var snapshot = catalog.GetSnapshot();

        snapshot.BackgroundPolling.RefreshInterval.Should().Be(TimeSpan.FromMinutes(5));
        snapshot.Providers.Should().NotBeEmpty();
    }

    [Test]
    [NonParallelizable]
    public void CatalogMarksProvidersMissingWhenPathAndAuthenticationSignalsAreCleared()
    {
        using var path = new EnvironmentVariableScope("PATH", string.Empty);
        using var openAi = new EnvironmentVariableScope("OPENAI_API_KEY", null);
        using var anthropic = new EnvironmentVariableScope("ANTHROPIC_API_KEY", null);
        using var githubToken = new EnvironmentVariableScope("GITHUB_TOKEN", null);
        using var githubHostToken = new EnvironmentVariableScope("GH_TOKEN", null);
        using var catalog = CreateCatalog();

        var providers = catalog.GetSnapshot().Providers;

        providers.Should().OnlyContain(provider =>
            provider.ReadinessState == ToolchainReadinessState.Missing &&
            provider.Provider.Status == ProviderConnectionStatus.Unavailable &&
            provider.AuthStatus == ToolchainAuthStatus.Missing &&
            provider.Diagnostics.Any(diagnostic => diagnostic.Name == "Launch" && diagnostic.Status == ToolchainDiagnosticStatus.Failed));
    }

    [TestCase("codex")]
    [TestCase("claude")]
    [TestCase("gh")]
    public void AvailableProvidersExposeVersionAndConnectionReadinessWhenInstalled(string commandName)
    {
        using var catalog = CreateCatalog();
        var provider = catalog.GetSnapshot().Providers.Single(item => item.Provider.CommandName == commandName);

        Assume.That(
            provider.Provider.Status,
            Is.EqualTo(ProviderConnectionStatus.Available),
            $"The '{commandName}' toolchain is not available in this environment.");

        provider.ExecutablePath.Should().NotBe("Not detected");
        provider.Diagnostics.Should().Contain(diagnostic => diagnostic.Name == "Launch" && diagnostic.Status == ToolchainDiagnosticStatus.Passed);
        provider.VersionStatus.Should().NotBe(ToolchainVersionStatus.Missing);
    }

    private static ToolchainCenterCatalog CreateCatalog()
    {
        return new ToolchainCenterCatalog(TimeProvider.System, startBackgroundPolling: false);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _variableName;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string variableName, string? value)
        {
            _variableName = variableName;
            _originalValue = Environment.GetEnvironmentVariable(variableName);
            Environment.SetEnvironmentVariable(variableName, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_variableName, _originalValue);
        }
    }
}
