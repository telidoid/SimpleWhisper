using System.Diagnostics;

namespace SimpleWhisper.Services;

public sealed class XdotoolTextPasteService : ITextPasteService
{
    private static readonly HashSet<string> TerminalWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "gnome-terminal", "gnome-terminal-server", "konsole", "xfce4-terminal",
        "alacritty", "kitty", "foot", "wezterm", "wezterm-gui",
        "xterm", "uxterm", "urxvt", "rxvt",
        "terminator", "tilix", "sakura", "guake", "tilda", "yakuake",
        "st", "st-256color", "cool-retro-term", "terminology",
        "deepin-terminal", "lxterminal", "mate-terminal", "qterminal",
        "contour", "rio", "ghostty", "tabby", "hyper",
    };

    private readonly IClipboardService _clipboardService;
    private string? _targetWindowId;
    private bool _targetIsTerminal;

    public XdotoolTextPasteService(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public bool IsAvailable => true;

    public void CaptureTargetWindow()
    {
        try
        {
            _targetWindowId = GetActiveWindowId();
            _targetIsTerminal = _targetWindowId is not null && IsTerminalWindow(_targetWindowId);
        }
        catch
        {
            _targetWindowId = null;
            _targetIsTerminal = false;
        }
    }

    public async Task PasteAsync(string text, CancellationToken ct = default)
    {
        await _clipboardService.SetTextAsync(text);
        await FocusCapturedWindowAsync(ct);
        await SendPasteKeystrokeAsync(_targetIsTerminal, ct);
    }

    private static string? GetActiveWindowId()
    {
        using var proc = Process.Start(new ProcessStartInfo("xdotool", ["getactivewindow"])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        });

        if (proc is null) return null;

        var output = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit();

        return IsValidWindowId(output, proc.ExitCode) ? output : null;
    }

    private static bool IsValidWindowId(string output, int exitCode)
        => exitCode == 0 && long.TryParse(output, out _);

    private static bool IsTerminalWindow(string windowId)
    {
        if (!long.TryParse(windowId, out _)) return false;

        using var proc = Process.Start(new ProcessStartInfo("xdotool",
            ["getwindowclassname", windowId])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        });

        if (proc is null) return false;

        var className = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit();

        return proc.ExitCode == 0 && TerminalWindowClasses.Contains(className);
    }

    private async Task FocusCapturedWindowAsync(CancellationToken ct)
    {
        if (_targetWindowId is null || !long.TryParse(_targetWindowId, out _)) return;

        using var proc = Process.Start(new ProcessStartInfo("xdotool",
            ["windowactivate", "--sync", _targetWindowId])
        {
            UseShellExecute = false,
        });

        if (proc is not null)
            await proc.WaitForExitAsync(ct);
    }

    private static async Task SendPasteKeystrokeAsync(bool useTerminalShortcut, CancellationToken ct)
    {
        var keystroke = useTerminalShortcut ? "ctrl+shift+v" : "ctrl+v";

        using var proc = Process.Start(new ProcessStartInfo("xdotool",
            ["key", "--clearmodifiers", keystroke])
        {
            UseShellExecute = false,
        });

        if (proc is not null)
            await proc.WaitForExitAsync(ct);
    }
}
