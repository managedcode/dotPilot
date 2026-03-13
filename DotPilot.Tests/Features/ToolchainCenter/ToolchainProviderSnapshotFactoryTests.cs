using System.Reflection;

namespace DotPilot.Tests.Features.ToolchainCenter;

public class ToolchainProviderSnapshotFactoryTests
{
    [Test]
    public void ResolveProviderStatusCoversUnavailableAuthenticationAndMisconfiguredBranches()
    {
        ResolveProviderStatus(isInstalled: false, authConfigured: false, toolAccessAvailable: false)
            .Should().Be(ProviderConnectionStatus.Unavailable);
        ResolveProviderStatus(isInstalled: true, authConfigured: false, toolAccessAvailable: false)
            .Should().Be(ProviderConnectionStatus.RequiresAuthentication);
        ResolveProviderStatus(isInstalled: true, authConfigured: true, toolAccessAvailable: false)
            .Should().Be(ProviderConnectionStatus.Misconfigured);
        ResolveProviderStatus(isInstalled: true, authConfigured: true, toolAccessAvailable: true)
            .Should().Be(ProviderConnectionStatus.Available);
    }

    [Test]
    public void ResolveReadinessStateCoversMissingActionRequiredLimitedAndReady()
    {
        ResolveReadinessState(isInstalled: false, authConfigured: false, toolAccessAvailable: false, installedVersion: string.Empty)
            .Should().Be(ToolchainReadinessState.Missing);
        ResolveReadinessState(isInstalled: true, authConfigured: false, toolAccessAvailable: true, installedVersion: "1.0.0")
            .Should().Be(ToolchainReadinessState.ActionRequired);
        ResolveReadinessState(isInstalled: true, authConfigured: true, toolAccessAvailable: false, installedVersion: "1.0.0")
            .Should().Be(ToolchainReadinessState.Limited);
        ResolveReadinessState(isInstalled: true, authConfigured: true, toolAccessAvailable: true, installedVersion: string.Empty)
            .Should().Be(ToolchainReadinessState.Limited);
        ResolveReadinessState(isInstalled: true, authConfigured: true, toolAccessAvailable: true, installedVersion: "1.0.0")
            .Should().Be(ToolchainReadinessState.Ready);
    }

    [Test]
    public void ResolveHealthStatusCoversBlockedWarningAndHealthy()
    {
        ResolveHealthStatus(isInstalled: false, authConfigured: false, toolAccessAvailable: false, installedVersion: string.Empty)
            .Should().Be(ToolchainHealthStatus.Blocked);
        ResolveHealthStatus(isInstalled: true, authConfigured: false, toolAccessAvailable: true, installedVersion: "1.0.0")
            .Should().Be(ToolchainHealthStatus.Blocked);
        ResolveHealthStatus(isInstalled: true, authConfigured: true, toolAccessAvailable: false, installedVersion: "1.0.0")
            .Should().Be(ToolchainHealthStatus.Warning);
        ResolveHealthStatus(isInstalled: true, authConfigured: true, toolAccessAvailable: true, installedVersion: string.Empty)
            .Should().Be(ToolchainHealthStatus.Warning);
        ResolveHealthStatus(isInstalled: true, authConfigured: true, toolAccessAvailable: true, installedVersion: "1.0.0")
            .Should().Be(ToolchainHealthStatus.Healthy);
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

    private static ProviderConnectionStatus ResolveProviderStatus(bool isInstalled, bool authConfigured, bool toolAccessAvailable)
    {
        return (ProviderConnectionStatus)InvokeFactoryMethod(
            "ResolveProviderStatus",
            isInstalled,
            authConfigured,
            toolAccessAvailable)!;
    }

    private static ToolchainReadinessState ResolveReadinessState(
        bool isInstalled,
        bool authConfigured,
        bool toolAccessAvailable,
        string installedVersion)
    {
        return (ToolchainReadinessState)InvokeFactoryMethod(
            "ResolveReadinessState",
            isInstalled,
            authConfigured,
            toolAccessAvailable,
            installedVersion)!;
    }

    private static ToolchainHealthStatus ResolveHealthStatus(
        bool isInstalled,
        bool authConfigured,
        bool toolAccessAvailable,
        string installedVersion)
    {
        return (ToolchainHealthStatus)InvokeFactoryMethod(
            "ResolveHealthStatus",
            isInstalled,
            authConfigured,
            toolAccessAvailable,
            installedVersion)!;
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
