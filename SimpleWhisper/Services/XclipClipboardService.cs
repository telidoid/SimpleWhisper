using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SimpleWhisper.Services;

// Avalonia's X11 clipboard is tied to MainWindow's X selection ownership, which
// silently no-ops when MainWindow is null (start-hidden path) and misbehaves on
// some compositors while the window is unmapped. Shell out to xclip instead so
// clipboard writes work regardless of window state.
public sealed class XclipClipboardService(ILogger<XclipClipboardService>? logger = null) : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("xclip", ["-selection", "clipboard"])
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                StandardInputEncoding = Encoding.UTF8,
            });

            if (proc is null)
            {
                logger?.LogWarning("xclip not available; clipboard write skipped");
                return;
            }

            await proc.StandardInput.WriteAsync(text);
            proc.StandardInput.Close();
            await proc.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "xclip clipboard write failed");
        }
    }
}
