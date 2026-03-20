using System.Runtime.InteropServices;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleWhisper.Services;
using SimpleWhisper.Services.Hotkey;

namespace SimpleWhisper;

internal static partial class Program
{
    private const string AppUserModelId = "SimpleWhisper";

    public static IHost AppHost { get; private set; } = null!;
    public static bool StartMinimized { get; private set; }

    [STAThread]
    public static async Task Main(string[] args)
    {
        StartMinimized = args.Contains("--minimized");
        if (OperatingSystem.IsWindows())
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

        AppHost = Host.CreateDefaultBuilder(args)
            .ConfigureSimpleWhisper()
            .Build();

        // Apply saved language before the UI is created so x:Static strings resolve correctly
        LocalizationService.ApplySavedCulture(AppHost.Services.GetRequiredService<IAppSettingsService>());

        // Register the global hotkey before the window opens so it's available immediately
        var hotkeyService = AppHost.Services.GetRequiredService<IGlobalHotkeyService>();
        try
        {
            await hotkeyService.StartAsync();
        }
        catch (Exception ex)
        {
            // Hotkey registration failing is non-fatal — the UI buttons still work
            await Console.Error.WriteLineAsync($"Global hotkey unavailable: {ex.Message}");
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        // Dispose all registered services (WhisperTranscriptionService, hotkey, etc.)
        AppHost.Dispose();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial void SetCurrentProcessExplicitAppUserModelID(string appId);
}