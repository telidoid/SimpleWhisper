using System.Runtime.InteropServices;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleWhisper.Services;
using SimpleWhisper.Services.Hotkey;
using Tmds.DBus.Protocol;

namespace SimpleWhisper;

internal static partial class Program
{
    private const string AppUserModelId = "SimpleWhisper";

    public static IHost AppHost { get; private set; } = null!;
    public static bool StartMinimized { get; private set; }
    public static bool IsSystemTrayAvailable { get; private set; } = true;

    [STAThread]
    public static async Task Main(string[] args)
    {
        StartMinimized = args.Contains("--minimized");
        if (OperatingSystem.IsWindows())
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

        AppHost = Host.CreateDefaultBuilder(args)
            .ConfigureSimpleWhisper()
            .Build();

        var settings = AppHost.Services.GetRequiredService<IAppSettingsService>();

        // Apply saved language before the UI is created so x:Static strings resolve correctly
        LocalizationService.ApplySavedCulture(settings);

        // Only probe DBus when the user actually wants the tray — saves up to 2s on
        // misconfigured Linux sessions where the probe would have to time out.
        if (settings.MinimizeToTray)
            IsSystemTrayAvailable = await DetectSystemTrayAsync();

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
        await AppHost.StopAsync();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    // Avalonia's Linux tray uses org.kde.StatusNotifierWatcher; silently no-ops when no host is
    // registered (stock GNOME without AppIndicator, bare wlroots compositors, etc.). Probe DBus
    // so we can avoid the "invisible orphan process" trap on autostart.
    private static async Task<bool> DetectSystemTrayAsync()
    {
        if (!OperatingSystem.IsLinux()) return true;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var address = DBusAddress.Session;
            if (address is null) return false;

            using var dbus = new DBusConnection(address);
            await dbus.ConnectAsync().AsTask().WaitAsync(cts.Token);

            var writer = dbus.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: "org.freedesktop.DBus",
                path: new ObjectPath("/org/freedesktop/DBus"),
                @interface: "org.freedesktop.DBus",
                member: "NameHasOwner",
                signature: "s");
            writer.WriteString("org.kde.StatusNotifierWatcher");

            return await dbus
                .CallMethodAsync(writer.CreateMessage(), static (m, _) => m.GetBodyReader().ReadBool(), (object?)null)
                .WaitAsync(cts.Token);
        }
        catch
        {
            return false;
        }
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial void SetCurrentProcessExplicitAppUserModelID(string appId);
}