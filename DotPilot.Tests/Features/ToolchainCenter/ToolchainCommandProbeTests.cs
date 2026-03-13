using System.Reflection;

namespace DotPilot.Tests.Features.ToolchainCenter;

public class ToolchainCommandProbeTests
{
    private const string NonExecutableContents = "not an executable";

    [Test]
    public void ReadVersionUsesStandardErrorWhenStandardOutputIsEmpty()
    {
        var (executablePath, arguments) = CreateShellCommand(
            OperatingSystem.IsWindows()
                ? "echo Claude Code version: 2.3.4 1>&2"
                : "printf 'Claude Code version: 2.3.4\\n' >&2");

        var version = ReadVersion(executablePath, arguments);

        version.Should().Be("2.3.4");
    }

    [Test]
    public void ReadVersionReturnsTheTrimmedFirstLineWhenNoVersionSeparatorExists()
    {
        var (executablePath, arguments) = CreateShellCommand(
            OperatingSystem.IsWindows()
                ? "(echo v9.8.7) & (echo ignored)"
                : "printf 'v9.8.7\\nignored\\n'");

        var version = ReadVersion(executablePath, arguments);

        version.Should().Be("v9.8.7");
    }

    [Test]
    public void ReadVersionReturnsEmptyWhenTheCommandFails()
    {
        var (executablePath, arguments) = CreateShellCommand(
            OperatingSystem.IsWindows()
                ? "echo boom 1>&2 & exit /b 1"
                : "printf 'boom\\n' >&2; exit 1");

        var version = ReadVersion(executablePath, arguments);

        version.Should().BeEmpty();
    }

    [Test]
    public void CanExecuteReturnsFalseWhenTheCommandFails()
    {
        var (executablePath, arguments) = CreateShellCommand(
            OperatingSystem.IsWindows()
                ? "exit /b 1"
                : "exit 1");

        var canExecute = CanExecute(executablePath, arguments);

        canExecute.Should().BeFalse();
    }

    [Test]
    public void CanExecuteReturnsTrueWhenTheCommandSucceeds()
    {
        var (executablePath, arguments) = CreateShellCommand(
            OperatingSystem.IsWindows()
                ? "exit /b 0"
                : "exit 0");

        var canExecute = CanExecute(executablePath, arguments);

        canExecute.Should().BeTrue();
    }

    [Test]
    public void ReadVersionReturnsEmptyWhenTheCommandTimesOut()
    {
        var (executablePath, arguments) = CreateShellCommand(
            OperatingSystem.IsWindows()
                ? "ping 127.0.0.1 -n 4 >nul"
                : "sleep 3");

        var version = ReadVersion(executablePath, arguments);

        version.Should().BeEmpty();
    }

    [Test]
    public void CanExecuteReturnsFalseWhenTheResolvedPathCannotBeLaunched()
    {
        var nonExecutablePath = Path.GetTempFileName();

        try
        {
            File.WriteAllText(nonExecutablePath, NonExecutableContents);

            CanExecute(nonExecutablePath, []).Should().BeFalse();
            ReadVersion(nonExecutablePath, []).Should().BeEmpty();
        }
        finally
        {
            File.Delete(nonExecutablePath);
        }
    }

    [Test]
    public void CanExecuteReturnsTrueWhenTheCommandProducesLargeRedirectedOutput()
    {
        var (executablePath, arguments) = CreateShellCommand(
            OperatingSystem.IsWindows()
                ? "for /L %i in (1,1,3000) do @echo output-line-%i"
                : "i=1; while [ $i -le 3000 ]; do printf 'output-line-%s\\n' \"$i\"; i=$((i+1)); done");

        var canExecute = CanExecute(executablePath, arguments);

        canExecute.Should().BeTrue();
    }

    private static string ReadVersion(string executablePath, IReadOnlyList<string> arguments)
    {
        return (string)InvokeProbeMethod("ReadVersion", executablePath, arguments);
    }

    private static bool CanExecute(string executablePath, IReadOnlyList<string> arguments)
    {
        return (bool)InvokeProbeMethod("CanExecute", executablePath, arguments);
    }

    private static object InvokeProbeMethod(string methodName, string executablePath, IReadOnlyList<string> arguments)
    {
        var probeType = typeof(ToolchainCenterCatalog).Assembly.GetType(
            "DotPilot.Runtime.Features.ToolchainCenter.ToolchainCommandProbe",
            throwOnError: true)!;
        var method = probeType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        return method.Invoke(null, [executablePath, arguments])!;
    }

    private static (string ExecutablePath, string[] Arguments) CreateShellCommand(string command)
    {
        return OperatingSystem.IsWindows()
            ? ("cmd.exe", ["/d", "/c", command])
            : ("/bin/sh", ["-c", command]);
    }
}
