using Microsoft.UI.Dispatching;

namespace DotPilot.Presentation;

public sealed class UiDispatcher
{
    private readonly DispatcherQueue? dispatcherQueue = TryGetDispatcherQueue();

    public void Execute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (dispatcherQueue is null || dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _ = dispatcherQueue.TryEnqueue(() => action());
    }

    private static DispatcherQueue? TryGetDispatcherQueue()
    {
        try
        {
            return DispatcherQueue.GetForCurrentThread();
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
