using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;

namespace SimpleWhisper.Services.Hotkey;

public sealed class AvaloniaNotificationService : INotificationService
{
    private WindowNotificationManager? _manager;

    public Task NotifyAsync(string text, CancellationToken ct = default)
    {
        var display = text.Length > 200 ? $"{text[..200]}..." : text;

        Dispatcher.UIThread.Post(() =>
        {
            _manager ??= CreateManager();
            _manager?.Show(new Notification("SimpleWhisper", display, NotificationType.Information));
        });

        return Task.CompletedTask;
    }

    private static WindowNotificationManager? CreateManager()
    {
        var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (mainWindow is null) return null;

        return new WindowNotificationManager(mainWindow)
        {
            Position = NotificationPosition.TopRight,
            MaxItems = 3
        };
    }
}
