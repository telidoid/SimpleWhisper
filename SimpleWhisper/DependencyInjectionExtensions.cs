using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleWhisper.Services;
using SimpleWhisper.Services.Hotkey;
using SimpleWhisper.ViewModels;

namespace SimpleWhisper;

internal static class DependencyInjectionExtensions
{
    public static IHostBuilder ConfigureSimpleWhisper(this IHostBuilder builder)
        => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IModelSelectionService, ModelSelectionService>();
            services.AddSingleton<IModelDownloadService, ModelDownloadService>();
            services.AddSingleton<IModelCatalogService, ModelCatalogService>();
            services.AddSingleton<IWhisperTranscriptionService, WhisperTranscriptionService>();
            services.AddTransient<IAudioRecordingService, AudioRecordingService>();
            services.AddSingleton<MainPageViewModel>();
            services.AddSingleton<ModelsPageViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            if (OperatingSystem.IsLinux() && IsWaylandSession())
            {
                services.AddSingleton<IGlobalHotkeyService, XdgPortalHotkeyService>();
                services.AddSingleton<INotificationService, FreedesktopNotificationService>();
            }
            else
            {
                services.AddSingleton<IGlobalHotkeyService, NullHotkeyService>();
                services.AddSingleton<INotificationService, NullNotificationService>();
            }
        });

    private static bool IsWaylandSession() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
}