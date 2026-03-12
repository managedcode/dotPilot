using Uno.UI.Hosting;

namespace DotPilot;

internal sealed class Program
{
    public static async Task Main(string[] _)
    {
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWebAssembly()
            .Build();

        await host.RunAsync();
    }
}
