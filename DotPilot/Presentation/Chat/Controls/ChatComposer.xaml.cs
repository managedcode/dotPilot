#if !__WASM__
using Windows.System;
#endif

namespace DotPilot.Presentation.Controls;

public sealed partial class ChatComposer : UserControl
{
    private const string ComposerInputAutomationId = "ChatComposerInput";
    private const string SendButtonAutomationId = "ChatComposerSendButton";
    private const string NewLineValue = "\n";
    private readonly ChatComposerModifierState _modifierState = new();

    public ChatComposer()
    {
        InitializeComponent();
        RegisterPropertyChangedCallback(TagProperty, OnBehaviorTagChanged);
        UpdateAcceptsReturn();
    }

    private void OnComposerInputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
#if __WASM__
        return;
#else
        if (sender is not TextBox textBox)
        {
            return;
        }

        _modifierState.RegisterKeyDown(args.Key);
        if (args.Key is not VirtualKey.Enter)
        {
            return;
        }

        var hasModifier = _modifierState.HasPressedModifier;

        var action = ChatComposerKeyboardPolicy.Resolve(
            behavior: CurrentSendBehavior,
            isEnterKey: true,
            hasModifier: hasModifier);
        if (!ChatComposerKeyboardPolicy.ShouldHandleInComposer(CurrentSendBehavior, action, hasModifier))
        {
            return;
        }

#if USE_UITESTS
        BrowserConsoleDiagnostics.Error(
            $"[DotPilot.ChatComposer] KeyDown invoked. HasModifier={hasModifier} Action={action} Behavior={CurrentSendBehavior}.");
#endif

        args.Handled = true;
        if (action is ChatComposerKeyboardAction.SendMessage)
        {
            ExecuteSubmitAction(textBox);
            return;
        }

        InsertNewLine(textBox);
#endif
    }

    private void OnComposerInputKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
#if !__WASM__
        _modifierState.RegisterKeyUp(args.Key);
#endif
    }

    private void OnComposerInputLostFocus(object sender, RoutedEventArgs e)
    {
        _modifierState.Reset();
    }

    private void OnBehaviorTagChanged(DependencyObject sender, DependencyProperty dependencyProperty)
    {
        if (!ReferenceEquals(sender, this) || dependencyProperty != TagProperty)
        {
            return;
        }

        UpdateAcceptsReturn();
        _ = SynchronizeBrowserKeyboardInteropAsync();
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        ChatComposerBrowserInterop.RegisterComposer(ComposerInputAutomationId, this);
        await SynchronizeBrowserKeyboardInteropAsync();
    }

    private async void OnUnloadedAsync(object sender, RoutedEventArgs e)
    {
        _modifierState.Reset();
        ChatComposerBrowserInterop.UnregisterComposer(ComposerInputAutomationId, this);
        await ChatComposerBrowserInterop.DisposeAsync(ComposerInputAutomationId);
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

    internal void SubmitFromBrowser()
    {
        ExecuteSubmitAction(ComposerInput);
    }

    internal void ApplyTextFromBrowser(string value, int selectionStart)
    {
        ArgumentNullException.ThrowIfNull(value);

        ComposerInput.Text = value;
        ComposerInput.SelectionStart = Math.Clamp(selectionStart, 0, value.Length);
        ComposerInput.SelectionLength = 0;
        SynchronizeComposerText(ComposerInput);
    }

    private static void SynchronizeComposerText(TextBox textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);

        var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
        bindingExpression?.UpdateSource();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Used on non-browser targets for desktop keyboard handling.")]
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
        SynchronizeComposerText(textBox);
    }

    private void UpdateAcceptsReturn()
    {
        if (ComposerInput is null)
        {
            return;
        }

        ComposerInput.AcceptsReturn = true;
    }

    private Task SynchronizeBrowserKeyboardInteropAsync()
    {
        return ChatComposerBrowserInterop.SynchronizeAsync(
            ComposerInputAutomationId,
            SendButtonAutomationId,
            CurrentSendBehavior);
    }

    private ComposerSendBehavior CurrentSendBehavior =>
        Tag is ComposerSendBehavior behavior
            ? behavior
            : ComposerSendBehavior.EnterSends;
}
