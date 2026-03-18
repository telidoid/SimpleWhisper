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
            services.AddSingleton<IAppSettingsService, AppSettingsService>();
            services.AddSingleton<IModelSelectionService, ModelSelectionService>();
            services.AddSingleton<IModelDownloadService, ModelDownloadService>();
            services.AddSingleton<IModelCatalogService, ModelCatalogService>();
            services.AddSingleton<IWhisperTranscriptionService, WhisperTranscriptionService>();
            services.AddTransient<IAudioRecordingService, AudioRecordingService>();
            services.AddSingleton<MainPageViewModel>();
            services.AddSingleton<ModelsPageViewModel>();
            services.AddSingleton<SettingsPageViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IGlobalHotkeyService, WindowsHotkeyService>();
                services.AddSingleton<INotificationService, AvaloniaNotificationService>();
                services.AddSingleton<ITextPasteService, WindowsTextPasteService>();
                services.AddSingleton<IClipboardService, WindowsClipboardService>();
            }
            else if (OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IGlobalHotkeyService, MacHotkeyService>();
                services.AddSingleton<INotificationService, MacNotificationService>();
                services.AddSingleton<ITextPasteService, MacTextPasteService>();
                services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
            }
            else if (OperatingSystem.IsLinux() && IsWaylandSession())
            {
                services.AddSingleton<IGlobalHotkeyService, XdgPortalHotkeyService>();
                services.AddSingleton<INotificationService, FreedesktopNotificationService>();
                services.AddSingleton<ITextPasteService, NullTextPasteService>();
                services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
            }
            else if (OperatingSystem.IsLinux())
            {
                services.AddSingleton<IGlobalHotkeyService, NullHotkeyService>();
                services.AddSingleton<INotificationService, FreedesktopNotificationService>();
                services.AddSingleton<ITextPasteService, XdotoolTextPasteService>();
                services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
            }
            else
            {
                services.AddSingleton<IGlobalHotkeyService, NullHotkeyService>();
                services.AddSingleton<INotificationService, NullNotificationService>();
                services.AddSingleton<ITextPasteService, NullTextPasteService>();
                services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
            }
        });

    private static bool IsWaylandSession() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
}