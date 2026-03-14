using System.Diagnostics;

namespace SimpleWhisper.Services.Hotkey;

public sealed class FreedesktopNotificationService : INotificationService
{
    public async Task NotifyAsync(string text, CancellationToken ct = default)
    {
        var display = text.Length > 200 ? $"{text[..200]}..." : text;

        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo("notify-send")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        proc.StartInfo.ArgumentList.Add("--app-name=SimpleWhisper");
        proc.StartInfo.ArgumentList.Add("--icon=microphone");
        proc.StartInfo.ArgumentList.Add("--expire-time=10000");
        proc.StartInfo.ArgumentList.Add("SimpleWhisper");
        proc.StartInfo.ArgumentList.Add(display);

        try
        {
            proc.Start();
            await proc.WaitForExitAsync(ct);
        }
        catch
        {
            // notify-send not installed or failed — silently ignore
        }
    }
}