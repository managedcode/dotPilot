using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace DotPilot.Presentation.Controls;

public sealed partial class ChatComposer : UserControl
{
    private const string NewLineValue = "\n";

    public ChatComposer()
    {
        InitializeComponent();
    }

    private void OnComposerInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var action = ChatComposerKeyboardPolicy.Resolve(
            isEnterKey: e.Key is VirtualKey.Enter,
            isShiftPressed: IsKeyPressed(VirtualKey.Shift),
            isAltPressed: IsKeyPressed(VirtualKey.Menu));
        if (action is ChatComposerKeyboardAction.SendMessage)
        {
            ExecuteSubmitCommand();
            e.Handled = true;
            return;
        }

        if (action is not ChatComposerKeyboardAction.InsertNewLine)
        {
            return;
        }

        InsertNewLine(textBox);
        e.Handled = true;
    }

    private void ExecuteSubmitCommand()
    {
        var command = SendButton.Command;
        var parameter = SendButton.CommandParameter;
        if (command?.CanExecute(parameter) != true)
        {
            return;
        }

        command.Execute(parameter);
    }

    private static bool IsKeyPressed(VirtualKey key)
    {
        return InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
    }

    private static void InsertNewLine(TextBox textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);

        var currentText = textBox.Text ?? string.Empty;
        var insertionIndex = Math.Clamp(textBox.SelectionStart, 0, currentText.Length);
        var selectionLength = Math.Clamp(textBox.SelectionLength, 0, currentText.Length - insertionIndex);
        var updatedText = currentText
            .Remove(insertionIndex, selectionLength)
            .Insert(insertionIndex, NewLineValue);

        textBox.Text = updatedText;
        textBox.SelectionStart = insertionIndex + NewLineValue.Length;
        textBox.SelectionLength = 0;
    }
}
