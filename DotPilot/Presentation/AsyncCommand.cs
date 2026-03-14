namespace DotPilot.Presentation;

public sealed class AsyncCommand(
    Func<object?, Task> executeAsync,
    Func<object?, bool>? canExecute = null) : ICommand
{
    private bool _isExecuting;

    public AsyncCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        : this(
            _ => executeAsync(),
            canExecute is null ? null : _ => canExecute())
    {
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (canExecute?.Invoke(parameter) ?? true);
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
            await executeAsync(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
