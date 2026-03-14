namespace DotPilot.UITests.Harness;

[TestFixture]
public sealed class BoundedCleanupTests
{
    private const string CleanupOperationName = "test cleanup";
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(50);

    [Test]
    public void WhenCleanupCompletesWithinTimeoutThenItSucceeds()
    {
        BoundedCleanup.Run(static () => { }, Timeout, CleanupOperationName);
    }

    [Test]
    public void WhenCleanupThrowsThenItWrapsTheFailure()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => BoundedCleanup.Run(
                static () => throw new InvalidOperationException("boom"),
                Timeout,
                CleanupOperationName));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void WhenCleanupTimesOutThenItFailsFast()
    {
        var exception = Assert.Throws<TimeoutException>(
            () => BoundedCleanup.Run(
                static () => Thread.Sleep(System.Threading.Timeout.Infinite),
                Timeout,
                CleanupOperationName));

        Assert.That(exception, Is.Not.Null);
    }
}
