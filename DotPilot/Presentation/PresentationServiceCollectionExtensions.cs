using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Presentation;

internal static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddPresentationModels(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<WorkspaceProjectionNotifier>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SecondModel>();
        services.AddSingleton<SecondViewModel>();
        services.AddSingleton<SettingsModel>();
        services.AddSingleton<SettingsViewModel>();

        return services;
    }
}
