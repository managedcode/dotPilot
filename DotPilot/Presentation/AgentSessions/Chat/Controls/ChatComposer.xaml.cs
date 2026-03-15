using DotPilot.Core.Features.AgentSessions;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace DotPilot.Presentation.Controls;

public sealed partial class ChatComposer : UserControl
{
    private const string NewLineValue = "\n";
    public static readonly DependencyProperty SendBehaviorProperty =
        DependencyProperty.Register(
            nameof(SendBehavior),
            typeof(ComposerSendBehavior),
            typeof(ChatComposer),
            new PropertyMetadata(ComposerSendBehavior.EnterSends));

    public ChatComposer()
    {
        InitializeComponent();
    }

    public ComposerSendBehavior SendBehavior
    {
        get => (ComposerSendBehavior)GetValue(SendBehaviorProperty);
        set => SetValue(SendBehaviorProperty, value);
    }

    private void OnComposerInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var action = ChatComposerKeyboardPolicy.Resolve(
            behavior: SendBehavior,
            isEnterKey: e.Key is VirtualKey.Enter,
            hasModifier: HasModifierKeyPressed());
        if (action is ChatComposerKeyboardAction.SendMessage)
        {
            ExecuteSubmitAction(textBox);
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

    private void OnSendButtonClick(object sender, RoutedEventArgs e)
    {
#if USE_UITESTS
        BrowserConsoleDiagnostics.Error("[DotPilot.ChatComposer] Send button click received.");
#endif
        ExecuteSubmitAction(ComposerInput);
    }

    private void ExecuteSubmitAction(TextBox textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);

        SynchronizeComposerText(textBox);
        var command = SendButton.Command;
        if (command is null)
        {
#if USE_UITESTS
            BrowserConsoleDiagnostics.Error("[DotPilot.ChatComposer] Submit ignored because SendButton.Command is null.");
#endif
            return;
        }

        var parameter = textBox.Text;
        var canExecute = command.CanExecute(parameter);
        if (!canExecute)
        {
#if USE_UITESTS
            BrowserConsoleDiagnostics.Error("[DotPilot.ChatComposer] Submit ignored because command.CanExecute returned false.");
#endif
            return;
        }

#if USE_UITESTS
        BrowserConsoleDiagnostics.Error("[DotPilot.ChatComposer] Submit command executing.");
#endif
        if (canExecute)
        {
            command.Execute(parameter);
#if USE_UITESTS
            BrowserConsoleDiagnostics.Error("[DotPilot.ChatComposer] Submit command executed.");
#endif
        }
    }

    private static void SynchronizeComposerText(TextBox textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);

        var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
        bindingExpression?.UpdateSource();
    }

    private static bool HasModifierKeyPressed()
    {
        return IsKeyPressed(VirtualKey.Shift) ||
            IsKeyPressed(VirtualKey.Menu) ||
            IsKeyPressed(VirtualKey.Control) ||
            IsKeyPressed(VirtualKey.LeftWindows) ||
            IsKeyPressed(VirtualKey.RightWindows);
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
