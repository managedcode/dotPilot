namespace DotPilot;

internal static partial class BrowserConsoleDiagnostics
{
    internal static void Info(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
#if __WASM__
#pragma warning disable CA1416
        JSImportMethods.Info(message);
#pragma warning restore CA1416
#endif
    }

    internal static void Error(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
#if __WASM__
#pragma warning disable CA1416
        JSImportMethods.Error(message);
#pragma warning restore CA1416
#endif
    }

#if __WASM__
    [System.Runtime.Versioning.SupportedOSPlatform("browser")]
    private static partial class JSImportMethods
    {
        [System.Runtime.InteropServices.JavaScript.JSImport("globalThis.console.info")]
        internal static partial void Info(string message);

        [System.Runtime.InteropServices.JavaScript.JSImport("globalThis.console.error")]
        internal static partial void Error(string message);
    }
#endif
}
