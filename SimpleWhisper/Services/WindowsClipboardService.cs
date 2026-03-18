using System.Runtime.InteropServices;

namespace SimpleWhisper.Services;

public sealed partial class WindowsClipboardService : IClipboardService
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    public Task SetTextAsync(string text)
    {
        return Task.Run(() =>
        {
            if (!OpenClipboard(0))
                return;

            try
            {
                EmptyClipboard();

                var chars = text.AsSpan();
                var byteCount = (nuint)((chars.Length + 1) * sizeof(char));
                var hGlobal = GlobalAlloc(GMEM_MOVEABLE, byteCount);
                if (hGlobal == 0) return;

                var ptr = GlobalLock(hGlobal);
                if (ptr == 0)
                {
                    GlobalFree(hGlobal);
                    return;
                }

                try
                {
                    unsafe
                    {
                        chars.CopyTo(new Span<char>((void*)ptr, chars.Length + 1));
                        ((char*)ptr)[chars.Length] = '\0';
                    }
                }
                finally
                {
                    GlobalUnlock(hGlobal);
                }

                if (SetClipboardData(CF_UNICODETEXT, hGlobal) == 0)
                    GlobalFree(hGlobal); // only free if SetClipboardData failed
            }
            finally
            {
                CloseClipboard();
            }
        });
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint hWndNewOwner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    private static partial nint SetClipboardData(uint uFormat, nint hMem);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint hMem);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalFree(nint hMem);
}
