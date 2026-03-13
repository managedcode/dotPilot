namespace DotPilot.Tests;

internal sealed class TemporaryWorkbenchDirectory : IDisposable
{
    private const string GitIgnoreFileName = ".gitignore";
    private const string GitIgnoreContent =
        """
        ignored/
        *.tmp
        """;

    private TemporaryWorkbenchDirectory(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public static TemporaryWorkbenchDirectory Create(bool includeSupportedFiles = true)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "dotpilot-workbench-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, GitIgnoreFileName), GitIgnoreContent);

        if (includeSupportedFiles)
        {
            Directory.CreateDirectory(Path.Combine(root, "docs"));
            Directory.CreateDirectory(Path.Combine(root, "src"));
            Directory.CreateDirectory(Path.Combine(root, "ignored"));

            File.WriteAllText(Path.Combine(root, "docs", "Architecture.md"), "# Architecture");
            File.WriteAllText(Path.Combine(root, "src", "MainPage.xaml"), "<Page />");
            File.WriteAllText(Path.Combine(root, "src", "SettingsPage.xaml"), "<Page />");
            File.WriteAllText(Path.Combine(root, "ignored", "Secret.cs"), "internal sealed class Secret {}");
            File.WriteAllText(Path.Combine(root, "notes.tmp"), "ignored");
        }

        return new(root);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
