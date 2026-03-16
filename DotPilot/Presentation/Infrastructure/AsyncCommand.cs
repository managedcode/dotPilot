using Microsoft.UI.Dispatching;

namespace DotPilot.Presentation;

public sealed class AsyncCommand(
    Func<object?, ValueTask> executeAsync,
    Func<object?, bool>? canExecute = null) : ICommand
{
    private bool _isExecuting;
    private readonly Func<object?, ValueTask> _executeAsync =
        executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
    private readonly Func<object?, bool>? _canExecute = canExecute;
    private readonly DispatcherQueue? _dispatcherQueue = TryGetDispatcherQueue();

    public AsyncCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        : this(
            _ => new ValueTask(executeAsync()),
            canExecute is null ? null : _ => canExecute())
    {
    }

    public AsyncCommand(Func<ValueTask> executeAsync, Func<bool>? canExecute = null)
        : this(
            _ => executeAsync(),
            canExecute is null ? null : _ => canExecute())
    {
    }

    public AsyncCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null)
        : this(
            parameter => new ValueTask(executeAsync(parameter)),
            canExecute)
    {
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _executeAsync(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _dispatcherQueue.TryEnqueue(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
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
