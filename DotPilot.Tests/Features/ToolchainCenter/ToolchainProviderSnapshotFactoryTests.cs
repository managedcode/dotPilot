using System.Reflection;

namespace DotPilot.Tests.Features.ToolchainCenter;

public class ToolchainProviderSnapshotFactoryTests
{
    [Test]
    public void ResolveProviderStatusCoversUnavailableAuthenticationAndMisconfiguredBranches()
    {
        ResolveProviderStatus(isInstalled: false, launchAvailable: false, authConfigured: false, toolAccessAvailable: false)
            .Should().Be(ProviderConnectionStatus.Unavailable);
        ResolveProviderStatus(isInstalled: true, launchAvailable: false, authConfigured: false, toolAccessAvailable: false)
            .Should().Be(ProviderConnectionStatus.Unavailable);
        ResolveProviderStatus(isInstalled: true, launchAvailable: true, authConfigured: false, toolAccessAvailable: false)
            .Should().Be(ProviderConnectionStatus.RequiresAuthentication);
        ResolveProviderStatus(isInstalled: true, launchAvailable: true, authConfigured: true, toolAccessAvailable: false)
            .Should().Be(ProviderConnectionStatus.Misconfigured);
        ResolveProviderStatus(isInstalled: true, launchAvailable: true, authConfigured: true, toolAccessAvailable: true)
            .Should().Be(ProviderConnectionStatus.Available);
    }

    [Test]
    public void ResolveReadinessStateCoversMissingActionRequiredLimitedAndReady()
    {
        ResolveReadinessState(isInstalled: false, launchAvailable: false, authConfigured: false, toolAccessAvailable: false, installedVersion: string.Empty)
            .Should().Be(ToolchainReadinessState.Missing);
        ResolveReadinessState(isInstalled: true, launchAvailable: false, authConfigured: false, toolAccessAvailable: true, installedVersion: "1.0.0")
            .Should().Be(ToolchainReadinessState.Missing);
        ResolveReadinessState(isInstalled: true, launchAvailable: true, authConfigured: false, toolAccessAvailable: true, installedVersion: "1.0.0")
            .Should().Be(ToolchainReadinessState.ActionRequired);
        ResolveReadinessState(isInstalled: true, launchAvailable: true, authConfigured: true, toolAccessAvailable: false, installedVersion: "1.0.0")
            .Should().Be(ToolchainReadinessState.Limited);
        ResolveReadinessState(isInstalled: true, launchAvailable: true, authConfigured: true, toolAccessAvailable: true, installedVersion: string.Empty)
            .Should().Be(ToolchainReadinessState.Limited);
        ResolveReadinessState(isInstalled: true, launchAvailable: true, authConfigured: true, toolAccessAvailable: true, installedVersion: "1.0.0")
            .Should().Be(ToolchainReadinessState.Ready);
    }

    [Test]
    public void ResolveHealthStatusCoversBlockedWarningAndHealthy()
    {
        ResolveHealthStatus(isInstalled: false, launchAvailable: false, authConfigured: false, toolAccessAvailable: false, installedVersion: string.Empty)
            .Should().Be(ToolchainHealthStatus.Blocked);
        ResolveHealthStatus(isInstalled: true, launchAvailable: false, authConfigured: false, toolAccessAvailable: true, installedVersion: "1.0.0")
            .Should().Be(ToolchainHealthStatus.Blocked);
        ResolveHealthStatus(isInstalled: true, launchAvailable: true, authConfigured: false, toolAccessAvailable: true, installedVersion: "1.0.0")
            .Should().Be(ToolchainHealthStatus.Blocked);
        ResolveHealthStatus(isInstalled: true, launchAvailable: true, authConfigured: true, toolAccessAvailable: false, installedVersion: "1.0.0")
            .Should().Be(ToolchainHealthStatus.Warning);
        ResolveHealthStatus(isInstalled: true, launchAvailable: true, authConfigured: true, toolAccessAvailable: true, installedVersion: string.Empty)
            .Should().Be(ToolchainHealthStatus.Warning);
        ResolveHealthStatus(isInstalled: true, launchAvailable: true, authConfigured: true, toolAccessAvailable: true, installedVersion: "1.0.0")
            .Should().Be(ToolchainHealthStatus.Healthy);
    }

    [Test]
    public void ResolveReadinessSummaryDistinguishesMissingInstallFromBrokenLaunch()
    {
        ResolveReadinessSummary("Codex CLI", isInstalled: false, launchAvailable: false, ToolchainReadinessState.Missing)
            .Should().Contain("not installed");
        ResolveReadinessSummary("Codex CLI", isInstalled: true, launchAvailable: false, ToolchainReadinessState.Missing)
            .Should().Contain("could not launch");
    }

    [Test]
    public void ResolveHealthSummaryPrefersInstallAndLaunchGuidanceBeforeAuth()
    {
        ResolveHealthSummary("Codex CLI", ToolchainHealthStatus.Blocked, isInstalled: false, launchAvailable: false, authConfigured: false)
            .Should().Contain("installed");
        ResolveHealthSummary("Codex CLI", ToolchainHealthStatus.Blocked, isInstalled: true, launchAvailable: false, authConfigured: false)
            .Should().Contain("start the CLI");
        ResolveHealthSummary("Codex CLI", ToolchainHealthStatus.Blocked, isInstalled: true, launchAvailable: true, authConfigured: false)
            .Should().Contain("authentication");
    }

    [Test]
    public void ResolveConfigurationStatusDistinguishesRequiredAndOptionalSignals()
    {
        var requiredSignal = CreateSignal(name: "REQUIRED_TOKEN", isRequiredForReadiness: true);
        var optionalSignal = CreateSignal(name: "OPTIONAL_ENDPOINT", isRequiredForReadiness: false);

        ResolveConfigurationStatus(requiredSignal, isConfigured: false)
            .Should().Be(ToolchainConfigurationStatus.Missing);
        ResolveConfigurationStatus(optionalSignal, isConfigured: false)
            .Should().Be(ToolchainConfigurationStatus.Partial);
        ResolveConfigurationStatus(optionalSignal, isConfigured: true)
            .Should().Be(ToolchainConfigurationStatus.Configured);
    }

    private static ProviderConnectionStatus ResolveProviderStatus(bool isInstalled, bool launchAvailable, bool authConfigured, bool toolAccessAvailable)
    {
        return (ProviderConnectionStatus)InvokeFactoryMethod(
            "ResolveProviderStatus",
            isInstalled,
            launchAvailable,
            authConfigured,
            toolAccessAvailable)!;
    }

    private static ToolchainReadinessState ResolveReadinessState(
        bool isInstalled,
        bool launchAvailable,
        bool authConfigured,
        bool toolAccessAvailable,
        string installedVersion)
    {
        return (ToolchainReadinessState)InvokeFactoryMethod(
            "ResolveReadinessState",
            isInstalled,
            launchAvailable,
            authConfigured,
            toolAccessAvailable,
            installedVersion)!;
    }

    private static ToolchainHealthStatus ResolveHealthStatus(
        bool isInstalled,
        bool launchAvailable,
        bool authConfigured,
        bool toolAccessAvailable,
        string installedVersion)
    {
        return (ToolchainHealthStatus)InvokeFactoryMethod(
            "ResolveHealthStatus",
            isInstalled,
            launchAvailable,
            authConfigured,
            toolAccessAvailable,
            installedVersion)!;
    }

    private static string ResolveReadinessSummary(
        string displayName,
        bool isInstalled,
        bool launchAvailable,
        ToolchainReadinessState readinessState)
    {
        return (string)InvokeFactoryMethod(
            "ResolveReadinessSummary",
            displayName,
            isInstalled,
            launchAvailable,
            readinessState)!;
    }

    private static string ResolveHealthSummary(
        string displayName,
        ToolchainHealthStatus healthStatus,
        bool isInstalled,
        bool launchAvailable,
        bool authConfigured)
    {
        return (string)InvokeFactoryMethod(
            "ResolveHealthSummary",
            displayName,
            healthStatus,
            isInstalled,
            launchAvailable,
            authConfigured)!;
    }

    private static ToolchainConfigurationStatus ResolveConfigurationStatus(object signal, bool isConfigured)
    {
        return (ToolchainConfigurationStatus)InvokeFactoryMethod("ResolveConfigurationStatus", signal, isConfigured)!;
    }

    private static object CreateSignal(string name, bool isRequiredForReadiness)
    {
        var signalType = typeof(ToolchainCenterCatalog).Assembly.GetType(
            "DotPilot.Runtime.Features.ToolchainCenter.ToolchainConfigurationSignal",
            throwOnError: true)!;

        return Activator.CreateInstance(
            signalType,
            name,
            "summary",
            ToolchainConfigurationKind.Secret,
            true,
            isRequiredForReadiness)!;
    }

    private static object? InvokeFactoryMethod(string methodName, params object[] arguments)
    {
        var factoryType = typeof(ToolchainCenterCatalog).Assembly.GetType(
            "DotPilot.Runtime.Features.ToolchainCenter.ToolchainProviderSnapshotFactory",
            throwOnError: true)!;
        var method = factoryType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)!;

        return method.Invoke(null, arguments);
    }
}
