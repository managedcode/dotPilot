namespace DotPilot.Presentation.Controls;

internal static class BoundCommandBridge
{
    public static void Execute(ICommand? command, object? parameter = null)
    {
        if (command?.CanExecute(parameter) != true)
        {
            return;
        }

        command.Execute(parameter);
    }
}
