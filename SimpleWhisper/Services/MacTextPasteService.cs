using System.Diagnostics;

namespace SimpleWhisper.Services;

public sealed class MacTextPasteService : ITextPasteService
{
    public bool IsAvailable => true;

    public async Task PasteAsync(string text, CancellationToken ct = default)
    {
        // Simulate Cmd+V via AppleScript (text is already on clipboard)
        using var proc = Process.Start(new ProcessStartInfo("osascript",
            ["-e", "tell application \"System Events\" to keystroke \"v\" using command down"])
        {
            UseShellExecute = false,
        });

        if (proc is not null)
            await proc.WaitForExitAsync(ct);
    }
}
