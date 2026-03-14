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

        var hotkeyService = AppHost.Services.GetRequiredService<IGlobalHotkeyService>();
        try
        {
            await hotkeyService.StartAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Global hotkey unavailable: {ex.Message}");
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        await hotkeyService.DisposeAsync();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}