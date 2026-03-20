#if WINDOWS
using CommunityToolkit.WinUI.Notifications;
#endif

namespace SimpleWhisper.Services.Hotkey;

public sealed class WindowsNotificationService : INotificationService
{
    public Task NotifyAsync(string text, CancellationToken ct = default)
    {
#if WINDOWS
        var display = text.Length > 200 ? $"{text[..200]}..." : text;

        try
        {
            new ToastContentBuilder()
                .AddText("SimpleWhisper")
                .AddText(display)
                .Show();
        }
        catch
        {
            // Silently ignore notification failures
        }
#endif
        return Task.CompletedTask;
    }
}
