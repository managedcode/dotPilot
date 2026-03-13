using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotPilot;

public partial class App : Application
{
    private const string StartupLogPrefix = "[DotPilot.Startup]";
    private const string ConstructorMarker = "App constructor initialized.";
    private const string OnLaunchedStartedMarker = "OnLaunched started.";
    private const string BuilderCreatedMarker = "Uno host builder created.";
    private const string NavigateStartedMarker = "Navigating to shell.";
    private const string NavigateCompletedMarker = "Shell navigation completed.";
#if !__WASM__
    private const string CenterMethodName = "Center";
    private const string WindowStartupLocationPropertyName = "WindowStartupLocation";
    private const string CenterScreenValueName = "CenterScreen";
    private const string ScreenWidthPropertyName = "ScreenWidthInRawPixels";
    private const string ScreenHeightPropertyName = "ScreenHeightInRawPixels";
    private const string WidthPropertyName = "Width";
    private const string HeightPropertyName = "Height";
    private const string PositionPropertyName = "Position";
    private const string XPropertyName = "X";
    private const string YPropertyName = "Y";
#endif

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        WriteStartupMarker(ConstructorMarker);
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Uno.Extensions APIs are used in a way that is safe for trimming in this template context.")]
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            WriteStartupMarker(OnLaunchedStartedMarker);
            var builder = this.CreateBuilder(args)
                // Add navigation support for toolkit controls such as TabBar and NavigationView
                .UseToolkitNavigation()
                .Configure(host => host
#if DEBUG
                    // Switch to Development environment when running in DEBUG
                    .UseEnvironment(Environments.Development)
#endif
                    .UseLogging(configure: (context, logBuilder) =>
                    {
                        // Configure log levels for different categories of logging
                        logBuilder
                            .SetMinimumLevel(
                                context.HostingEnvironment.IsDevelopment() ?
                                    LogLevel.Information :
                                    LogLevel.Warning)

                            // Default filters for core Uno Platform namespaces
                            .CoreLogLevel(LogLevel.Warning);

                        // Uno Platform namespace filter groups
                        // Uncomment individual methods to see more detailed logging
                        //// Generic Xaml events
                        //logBuilder.XamlLogLevel(LogLevel.Debug);
                        //// Layout specific messages
                        //logBuilder.XamlLayoutLogLevel(LogLevel.Debug);
                        //// Storage messages
                        //logBuilder.StorageLogLevel(LogLevel.Debug);
                        //// Binding related messages
                        //logBuilder.XamlBindingLogLevel(LogLevel.Debug);
                        //// Binder memory references tracking
                        //logBuilder.BinderMemoryReferenceLogLevel(LogLevel.Debug);
                        //// DevServer and HotReload related
                        //logBuilder.HotReloadCoreLogLevel(LogLevel.Information);
                        //// Debug JS interop
                        //logBuilder.WebAssemblyLogLevel(LogLevel.Debug);

                    }, enableUnoLogging: true)
                    .UseConfiguration(configure: configBuilder =>
                        configBuilder
                            .EmbeddedSource<App>()
                            .Section<AppConfig>()
                    )
                    // Enable localization (see appsettings.json for supported languages)
                    .UseLocalization()
                    .UseHttp((context, services) =>
                    {
#if DEBUG
                        // DelegatingHandler will be automatically injected
                        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                            .AddTransient<DelegatingHandler, DotPilot.Runtime.Features.HttpDiagnostics.DebugHttpHandler>(services);
#endif

                    })
                    .ConfigureServices((context, services) =>
                    {
                        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                            .AddSingleton<
                                DotPilot.Core.Features.Workbench.IWorkbenchCatalog,
                                DotPilot.Runtime.Features.Workbench.WorkbenchCatalog>(services);
                        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                            .AddSingleton<
                                DotPilot.Core.Features.RuntimeFoundation.IAgentRuntimeClient,
                                DotPilot.Runtime.Features.RuntimeFoundation.DeterministicAgentRuntimeClient>(services);
                        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                            .AddSingleton<
                                DotPilot.Core.Features.RuntimeFoundation.IRuntimeFoundationCatalog,
                                DotPilot.Runtime.Features.RuntimeFoundation.RuntimeFoundationCatalog>(services);
                        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                            .AddSingleton<
                                DotPilot.Core.Features.ToolchainCenter.IToolchainCenterCatalog,
                                DotPilot.Runtime.Features.ToolchainCenter.ToolchainCenterCatalog>(services);
                        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                            .AddTransient<ShellViewModel>(services);
                        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                            .AddTransient<MainViewModel>(services);
                        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                            .AddTransient<SecondViewModel>(services);
                        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                            .AddTransient<SettingsViewModel>(services);
                    })
                    .UseNavigation(RegisterRoutes)
                );
            WriteStartupMarker(BuilderCreatedMarker);
            MainWindow = builder.Window;

#if DEBUG
#if !__WASM__
            MainWindow.UseStudio();
#endif
#endif
            MainWindow.SetWindowIcon();
            WriteStartupMarker(NavigateStartedMarker);
            Host = await builder.NavigateAsync<Shell>();
            WriteStartupMarker(NavigateCompletedMarker);
#if !__WASM__
            CenterDesktopWindow(MainWindow);
#endif
        }
        catch (Exception exception)
        {
            WriteStartupError(exception);
            throw;
        }
    }

    private static void WriteStartupMarker(string message)
    {
        var formattedMessage = $"{StartupLogPrefix} {message}";
        Console.WriteLine(formattedMessage);
        BrowserConsoleDiagnostics.Info(formattedMessage);
    }

    private static void WriteStartupError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var formattedMessage = $"{StartupLogPrefix} ERROR {exception}";
        Console.Error.WriteLine(formattedMessage);
        BrowserConsoleDiagnostics.Error(formattedMessage);
    }

#if !__WASM__
    private static void CenterDesktopWindow(Window window)
    {
        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()))
        {
            return;
        }

        var nativeWindow = Uno.UI.Xaml.WindowHelper.GetNativeWindow(window);
        if (nativeWindow is null)
        {
            return;
        }

        if (TryInvokeCenter(nativeWindow) || TrySetCenteredStartupLocation(nativeWindow))
        {
            return;
        }

        _ = TryCenterWithRawScreenMetrics(nativeWindow);
    }

    private static bool TryInvokeCenter(object nativeWindow)
    {
        var centerMethod = nativeWindow
            .GetType()
            .GetMethod(
                CenterMethodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (centerMethod is null || centerMethod.GetParameters().Length != 0)
        {
            return false;
        }

        centerMethod.Invoke(nativeWindow, []);
        return true;
    }

    private static bool TrySetCenteredStartupLocation(object nativeWindow)
    {
        var startupLocationProperty = nativeWindow
            .GetType()
            .GetProperty(
                WindowStartupLocationPropertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

        if (startupLocationProperty?.CanWrite != true || !startupLocationProperty.PropertyType.IsEnum)
        {
            return false;
        }

        var centerValue = Enum.GetNames(startupLocationProperty.PropertyType)
            .FirstOrDefault(name => string.Equals(name, CenterScreenValueName, StringComparison.Ordinal));

        if (centerValue is null)
        {
            return false;
        }

        startupLocationProperty.SetValue(nativeWindow, Enum.Parse(startupLocationProperty.PropertyType, centerValue));
        return true;
    }

    private static bool TryCenterWithRawScreenMetrics(object nativeWindow)
    {
        if (!TryGetNumericPropertyValue(nativeWindow, ScreenWidthPropertyName, out var screenWidth) ||
            !TryGetNumericPropertyValue(nativeWindow, ScreenHeightPropertyName, out var screenHeight) ||
            !TryGetNumericPropertyValue(nativeWindow, WidthPropertyName, out var width) ||
            !TryGetNumericPropertyValue(nativeWindow, HeightPropertyName, out var height))
        {
            return false;
        }

        var positionProperty = nativeWindow
            .GetType()
            .GetProperty(
                PositionPropertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (positionProperty?.CanWrite != true)
        {
            return false;
        }

        var positionValue = positionProperty.GetValue(nativeWindow) ?? Activator.CreateInstance(positionProperty.PropertyType);
        if (positionValue is null)
        {
            return false;
        }

        var targetX = Math.Max(0, (screenWidth - width) / 2);
        var targetY = Math.Max(0, (screenHeight - height) / 2);

        if (!TrySetNumericPropertyValue(positionValue, XPropertyName, targetX) ||
            !TrySetNumericPropertyValue(positionValue, YPropertyName, targetY))
        {
            return false;
        }

        positionProperty.SetValue(nativeWindow, positionValue);
        return true;
    }

    private static bool TryGetNumericPropertyValue(object target, string propertyName, out int value)
    {
        var property = target
            .GetType()
            .GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (property is null)
        {
            value = 0;
            return false;
        }

        var propertyValue = property.GetValue(target);
        if (propertyValue is null)
        {
            value = 0;
            return false;
        }

        value = Convert.ToInt32(propertyValue, System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TrySetNumericPropertyValue(object target, string propertyName, int value)
    {
        var property = target
            .GetType()
            .GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (property?.CanWrite != true)
        {
            return false;
        }

        var convertedValue = Convert.ChangeType(value, property.PropertyType, System.Globalization.CultureInfo.InvariantCulture);
        property.SetValue(target, convertedValue);
        return true;
    }
#endif

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap(ViewModel: typeof(ShellViewModel)),
            new ViewMap<MainPage, MainViewModel>(),
            new ViewMap<SecondPage, SecondViewModel>(),
            new ViewMap<SettingsPage, SettingsViewModel>()
        );

        routes.Register(
            new RouteMap("", View: views.FindByViewModel<ShellViewModel>(),
                Nested:
                [
                    new ("Main", View: views.FindByViewModel<MainViewModel>(), IsDefault:true),
                    new ("Second", View: views.FindByViewModel<SecondViewModel>()),
                    new ("Settings", View: views.FindByViewModel<SettingsViewModel>()),
                ]
            )
        );
    }
}
