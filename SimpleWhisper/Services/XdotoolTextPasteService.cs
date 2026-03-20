using System.Diagnostics;

namespace SimpleWhisper.Services;

public sealed class XdotoolTextPasteService : ITextPasteService
{
    private readonly IClipboardService _clipboardService;
    private string? _targetWindowId;

    public XdotoolTextPasteService(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public bool IsAvailable => true;

    public void CaptureTargetWindow()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("xdotool", ["getactivewindow"])
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });

            if (proc is not null)
            {
                _targetWindowId = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                    _targetWindowId = null;
            }
        }
        catch
        {
            _targetWindowId = null;
        }
    }

    public async Task PasteAsync(string text, CancellationToken ct = default)
    {
        await _clipboardService.SetTextAsync(text);

        // Restore focus to the window that was active when recording started
        if (_targetWindowId is not null)
        {
            using var focus = Process.Start(new ProcessStartInfo("xdotool",
                ["windowactivate", "--sync", _targetWindowId])
            {
                UseShellExecute = false,
            });

            if (focus is not null)
                await focus.WaitForExitAsync(ct);
        }

        // Send Ctrl+V to paste from clipboard
        using var paste = Process.Start(new ProcessStartInfo("xdotool",
            ["key", "--clearmodifiers", "ctrl+v"])
        {
            UseShellExecute = false,
        });

        if (paste is not null)
            await paste.WaitForExitAsync(ct);
    }
}
