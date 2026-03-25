using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Presentation;

internal static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddPresentationModels(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<WorkspaceProjectionNotifier>();
        services.AddSingleton<ShellNavigationNotifier>();
        services.AddSingleton<SessionSelectionNotifier>();
        services.AddSingleton(new OperatorPreferencesStorageOptions());
        services.AddSingleton<IOperatorPreferencesStore, LocalOperatorPreferencesStore>();
        services.AddSingleton<ILocalModelPathPicker, DesktopLocalModelPathPicker>();
        services.AddSingleton<UiDispatcher>();
        services.AddSingleton<DesktopSleepPreventionService>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<ChatModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<AgentBuilderModel>();
        services.AddSingleton<AgentBuilderViewModel>();
        services.AddSingleton<SettingsModel>();
        services.AddSingleton<SettingsViewModel>();

        return services;
    }
}
