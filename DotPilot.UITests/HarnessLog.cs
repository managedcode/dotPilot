namespace DotPilot.UITests;

internal static class HarnessLog
{
    private const string Prefix = "[DotPilot.UITests]";

    public static void Write(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Console.WriteLine($"{Prefix} {message}");
    }
}
