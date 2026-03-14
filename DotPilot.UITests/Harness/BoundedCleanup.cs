namespace DotPilot.UITests.Harness;

internal static class BoundedCleanup
{
    private const string CleanupFailureMessagePrefix = "Cleanup for '";
    private const string CleanupFailureMessageSuffix = "' failed.";
    private const string CleanupThreadNamePrefix = "DotPilot.UITests cleanup: ";
    private const string CleanupTimeoutMessagePrefix = "Timed out while waiting for '";
    private const string CleanupTimeoutMessageMiddle = "' cleanup to finish within ";
    private const string CleanupTimeoutMessageSuffix = ".";

    public static void Run(Action cleanupAction, TimeSpan timeout, string operationName)
    {
        ArgumentNullException.ThrowIfNull(cleanupAction);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        using var cleanupCompleted = new ManualResetEventSlim(false);
        Exception? cleanupException = null;

        var cleanupThread = new Thread(() =>
        {
            try
            {
                cleanupAction();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }
            finally
            {
                cleanupCompleted.Set();
            }
        })
        {
            IsBackground = true,
            Name = $"{CleanupThreadNamePrefix}{operationName}",
        };

        cleanupThread.Start();

        if (!cleanupCompleted.Wait(timeout))
        {
            throw new TimeoutException(
                $"{CleanupTimeoutMessagePrefix}{operationName}{CleanupTimeoutMessageMiddle}{timeout}{CleanupTimeoutMessageSuffix}");
        }

        if (cleanupException is not null)
        {
            throw new InvalidOperationException(
                $"{CleanupFailureMessagePrefix}{operationName}{CleanupFailureMessageSuffix}",
                cleanupException);
        }
    }
}
