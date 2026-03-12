using System.Net;
using System.Net.Sockets;

namespace DotPilot.UITests;

internal static class BrowserTestEnvironment
{
    private const string BrowserBaseUriEnvironmentVariableName = "DOTPILOT_UITEST_BASE_URI";
    private const string DefaultScheme = "http";
    private const string DefaultHost = "127.0.0.1";

    public static string WebAssemblyUri { get; } = ResolveWebAssemblyUri();

    public static string WebAssemblyUrlsValue => WebAssemblyUri.TrimEnd('/');

    private static string ResolveWebAssemblyUri()
    {
        var configuredUri = Environment.GetEnvironmentVariable(BrowserBaseUriEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(configuredUri) &&
            Uri.TryCreate(configuredUri, UriKind.Absolute, out var absoluteUri))
        {
            return NormalizeUri(absoluteUri);
        }

        return NormalizeUri(CreateLoopbackUri(GetFreeTcpPort()));
    }

    private static Uri CreateLoopbackUri(int port)
    {
        return new UriBuilder(DefaultScheme, DefaultHost, port).Uri;
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string NormalizeUri(Uri uri)
    {
        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri.AbsoluteUri
            : $"{uri.AbsoluteUri}/";
    }
}
