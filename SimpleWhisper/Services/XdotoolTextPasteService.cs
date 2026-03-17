using System.Diagnostics;

namespace SimpleWhisper.Services;

public sealed class XdotoolTextPasteService : ITextPasteService
{
    public bool IsAvailable => true;

    public async Task PasteAsync(string text, CancellationToken ct = default)
    {
        using var proc = Process.Start(new ProcessStartInfo("xdotool", ["type", "--clearmodifiers", "--", text])
        {
            UseShellExecute = false,
        });

        if (proc is not null)
            await proc.WaitForExitAsync(ct);
    }
}
