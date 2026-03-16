namespace DotPilot.Presentation.Controls;

internal static partial class ChatComposerBrowserInterop
{
    private static readonly Dictionary<string, WeakReference<ChatComposer>> RegisteredComposers = [];

    internal static void RegisterComposer(string inputAutomationId, ChatComposer composer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputAutomationId);
        ArgumentNullException.ThrowIfNull(composer);

        RegisteredComposers[inputAutomationId] = new WeakReference<ChatComposer>(composer);
    }

    internal static void UnregisterComposer(string inputAutomationId, ChatComposer composer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputAutomationId);
        ArgumentNullException.ThrowIfNull(composer);

        if (!RegisteredComposers.TryGetValue(inputAutomationId, out var reference) ||
            !reference.TryGetTarget(out var target) ||
            !ReferenceEquals(target, composer))
        {
            return;
        }

        RegisteredComposers.Remove(inputAutomationId);
    }

    internal static void SubmitRegisteredComposer(string inputAutomationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputAutomationId);

        if (!RegisteredComposers.TryGetValue(inputAutomationId, out var reference))
        {
            return;
        }

        if (!reference.TryGetTarget(out var composer))
        {
            RegisteredComposers.Remove(inputAutomationId);
            return;
        }

        composer.SubmitFromBrowser();
    }

    internal static void ApplyTextForRegisteredComposer(string inputAutomationId, string value, int selectionStart)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputAutomationId);
        ArgumentNullException.ThrowIfNull(value);

        if (!RegisteredComposers.TryGetValue(inputAutomationId, out var reference))
        {
            return;
        }

        if (!reference.TryGetTarget(out var composer))
        {
            RegisteredComposers.Remove(inputAutomationId);
            return;
        }

        composer.ApplyTextFromBrowser(value, selectionStart);
    }

    internal static async Task SynchronizeAsync(
        string inputAutomationId,
        string sendButtonAutomationId,
        ComposerSendBehavior behavior)
    {
#if __WASM__
#pragma warning disable CA1416
        await EnsureInitializedAsync();
        JSImportMethods.Synchronize(inputAutomationId, sendButtonAutomationId, behavior.ToString());
#pragma warning restore CA1416
#endif
    }

    internal static async Task DisposeAsync(string inputAutomationId)
    {
#if __WASM__
#pragma warning disable CA1416
        await EnsureInitializedAsync();
        JSImportMethods.Dispose(inputAutomationId);
#pragma warning restore CA1416
#endif
    }

#if __WASM__
    private const string ModuleName = "DotPilotChatComposer";
    private const string ModulePath = "/scripts/ChatComposerBrowserInterop.js";
    private static Task? _moduleImportTask;

    [System.Runtime.Versioning.SupportedOSPlatform("browser")]
    private static Task EnsureInitializedAsync()
    {
        _moduleImportTask ??= System.Runtime.InteropServices.JavaScript.JSHost
            .ImportAsync(ModuleName, ModulePath)
            .AsTask();
        return _moduleImportTask;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("browser")]
    private static partial class JSImportMethods
    {
        [System.Runtime.InteropServices.JavaScript.JSImport("synchronize", ModuleName)]
        internal static partial void Synchronize(string inputAutomationId, string sendButtonAutomationId, string behavior);

        [System.Runtime.InteropServices.JavaScript.JSImport("dispose", ModuleName)]
        internal static partial void Dispose(string inputAutomationId);
    }
#endif
}

#if __WASM__
[System.Runtime.Versioning.SupportedOSPlatform("browser")]
public static partial class ChatComposerBrowserExports
{
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static void SubmitMessage(string inputAutomationId)
    {
        ChatComposerBrowserInterop.SubmitRegisteredComposer(inputAutomationId);
    }

    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static void ApplyText(string inputAutomationId, string value, int selectionStart)
    {
        ChatComposerBrowserInterop.ApplyTextForRegisteredComposer(inputAutomationId, value, selectionStart);
    }
}
#endif
