using System.Diagnostics;

namespace SimpleWhisper.Services;

public sealed class MacTextPasteService : ITextPasteService
{
    private readonly IClipboardService _clipboardService;

    public MacTextPasteService(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public bool IsAvailable => true;

    public async Task PasteAsync(string text, CancellationToken ct = default)
    {
        await _clipboardService.SetTextAsync(text);

        using var proc = Process.Start(new ProcessStartInfo("osascript",
            ["-e", "tell application \"System Events\" to keystroke \"v\" using command down"])
        {
            UseShellExecute = false,
        });

        if (proc is not null)
            await proc.WaitForExitAsync(ct);
    }
}
