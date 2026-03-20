#if WINDOWS
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
#endif

namespace SimpleWhisper.Services.Hotkey;

public sealed class WindowsNotificationService : INotificationService
{
    public Task NotifyAsync(string text, CancellationToken ct = default)
    {
#if WINDOWS
        var display = text.Length > 200 ? $"{text[..200]}..." : text;
        var escaped = display
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");

        try
        {
            var xml = new XmlDocument();
            xml.LoadXml($"""
                <toast>
                    <visual>
                        <binding template="ToastGeneric">
                            <text>SimpleWhisper</text>
                            <text>{escaped}</text>
                        </binding>
                    </visual>
                </toast>
                """);

            var notification = new ToastNotification(xml);
            ToastNotificationManager.CreateToastNotifier("SimpleWhisper").Show(notification);
        }
        catch
        {
            // Silently ignore notification failures
        }
#endif
        return Task.CompletedTask;
    }
}
