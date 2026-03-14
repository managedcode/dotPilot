namespace DotPilot.UITests.Harness;

[TestFixture]
public sealed class BrowserTestHostTests
{
    [Test]
    public void RunArgumentsKeepUiAutomationEnabledWithoutDisablingBuildChecks()
    {
        const string projectPath = "/repo/DotPilot/DotPilot.csproj";

        var arguments = BrowserTestHost.CreateRunArguments(projectPath);

        Assert.That(arguments, Does.Contain("run"));
        Assert.That(arguments, Does.Contain("-c"));
        Assert.That(arguments, Does.Contain("Release"));
        Assert.That(arguments, Does.Contain("-f"));
        Assert.That(arguments, Does.Contain("net10.0-browserwasm"));
        Assert.That(arguments, Does.Contain("-p:IsUiAutomationMappingEnabled=True"));
        Assert.That(arguments, Does.Contain("--project"));
        Assert.That(arguments, Does.Contain(projectPath));
        Assert.That(arguments, Does.Contain("--no-launch-profile"));
        Assert.That(arguments, Does.Not.Contain("--no-build"));
    }
}
