using System.Diagnostics;

namespace SimpleWhisper.Services.Hotkey;

public sealed class MacNotificationService : INotificationService
{
    public async Task NotifyAsync(string text, CancellationToken ct = default)
    {
        var display = text.Length > 200 ? $"{text[..200]}..." : text;
        var escaped = display.Replace("\\", "\\\\").Replace("\"", "\\\"");

        using var proc = Process.Start(new ProcessStartInfo("osascript",
            ["-e", $"display notification \"{escaped}\" with title \"SimpleWhisper\""])
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (proc is not null)
        {
            try
            {
                await proc.WaitForExitAsync(ct);
            }
            catch
            {
                // silently ignore
            }
        }
    }
}
