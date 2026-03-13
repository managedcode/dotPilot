using System.Reflection;

namespace DotPilot.Tests.Features.RuntimeFoundation;

public class ProviderToolchainProbeTests
{
    private const string DotnetCommandName = "dotnet";

    [Test]
    public void ProbeReturnsAvailableWhenTheCommandExistsOnPath()
    {
        var descriptor = Probe("Dotnet CLI", DotnetCommandName, requiresExternalToolchain: true);

        descriptor.Status.Should().Be(ProviderConnectionStatus.Available);
        descriptor.CommandName.Should().Be(DotnetCommandName);
        descriptor.RequiresExternalToolchain.Should().BeTrue();
        descriptor.StatusSummary.Should().Be("Dotnet CLI is available on PATH.");
    }

    [Test]
    public void ProbeReturnsUnavailableWhenTheCommandDoesNotExistOnPath()
    {
        var missingCommandName = $"missing-{Guid.NewGuid():N}";

        var descriptor = Probe("Missing CLI", missingCommandName, requiresExternalToolchain: true);

        descriptor.Status.Should().Be(ProviderConnectionStatus.Unavailable);
        descriptor.CommandName.Should().Be(missingCommandName);
        descriptor.StatusSummary.Should().Be("Missing CLI is not on PATH.");
    }

    [Test]
    public void ResolveExecutablePathFindsExistingExecutablesOnPath()
    {
        var executablePath = ResolveExecutablePath(DotnetCommandName);

        executablePath.Should().NotBeNullOrWhiteSpace();
        File.Exists(executablePath).Should().BeTrue();
    }

    private static ProviderDescriptor Probe(string displayName, string commandName, bool requiresExternalToolchain)
    {
        return (ProviderDescriptor)(InvokeProbeMethod("Probe", displayName, commandName, requiresExternalToolchain)
            ?? throw new InvalidOperationException("ProviderToolchainProbe.Probe returned null."));
    }

    private static string? ResolveExecutablePath(string commandName)
    {
        return (string?)InvokeProbeMethod("ResolveExecutablePath", commandName);
    }

    private static object? InvokeProbeMethod(string methodName, params object[] arguments)
    {
        var probeType = typeof(RuntimeFoundationCatalog).Assembly.GetType(
            "DotPilot.Runtime.Features.RuntimeFoundation.ProviderToolchainProbe",
            throwOnError: true)!;
        var method = probeType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        return method.Invoke(null, arguments);
    }
}
