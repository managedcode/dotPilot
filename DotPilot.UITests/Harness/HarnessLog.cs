namespace DotPilot.UITests.Harness;

internal static class HarnessLog
{
    private const string Prefix = "[DotPilot.UITests]";
    private const string LogFileName = "dotpilot-uitests-harness.log";
    private static readonly System.Threading.Lock SyncRoot = new();
    private static readonly string LogFilePath = Path.Combine(Path.GetTempPath(), LogFileName);

    public static void Write(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var logLine = $"{Prefix} {DateTimeOffset.UtcNow:O} {message}";
        Console.WriteLine(logLine);

        lock (SyncRoot)
        {
            File.AppendAllText(LogFilePath, string.Concat(logLine, Environment.NewLine));
        }
    }
}
