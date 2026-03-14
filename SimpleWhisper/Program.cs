using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleWhisper.Services.Hotkey;

namespace SimpleWhisper;

internal static class Program
{
    public static IHost AppHost { get; private set; } = null!;

    [STAThread]
    public static async Task Main(string[] args)
    {
        AppHost = Host.CreateDefaultBuilder(args)
            .ConfigureSimpleWhisper()
            .Build();

        // Register the global hotkey before the window opens so it's available immediately
        var hotkeyService = AppHost.Services.GetRequiredService<IGlobalHotkeyService>();
        try
        {
            await hotkeyService.StartAsync();
        }
        catch (Exception ex)
        {
            // Hotkey registration failing is non-fatal — the UI buttons still work
            Console.Error.WriteLine($"Global hotkey unavailable: {ex.Message}");
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
}