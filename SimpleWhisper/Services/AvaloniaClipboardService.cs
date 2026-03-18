using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace SimpleWhisper.Services;

public sealed class AvaloniaClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        var clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.Clipboard;
        if (clipboard != null)
            await clipboard.SetTextAsync(text);
    }
}
