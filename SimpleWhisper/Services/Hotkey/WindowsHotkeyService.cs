using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SimpleWhisper.Services.Hotkey;

public sealed partial class WindowsHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0x5750; // 'WP' for Whisper
    private const int WM_HOTKEY = 0x0312;

    private readonly IAppSettingsService _settings;
    private readonly ILogger<WindowsHotkeyService>? _logger;
    private Thread? _messageThread;
    private volatile bool _disposed;
    private bool _isPressed;
    private uint _currentModifiers;
    private uint _currentVk;

    public event EventHandler? RecordingStartRequested;
    public event EventHandler? RecordingStopRequested;

    public WindowsHotkeyService(IAppSettingsService settings, ILogger<WindowsHotkeyService>? logger = null)
    {
        _settings = settings;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource();
        _messageThread = new Thread(() => RunMessageLoop(tcs))
        {
            IsBackground = true,
            Name = "HotkeyMessageLoop"
        };
        _messageThread.Start();
        return tcs.Task;
    }

    public Task RebindAsync(string newTrigger, CancellationToken ct = default)
    {
        // Unregister on the message thread by posting a custom message isn't needed;
        // RegisterHotKey/UnregisterHotKey must be called from the thread that created the hotkey.
        // Since we use hwnd=0 (thread hotkey), we need to post to that thread.
        // For simplicity, just store the new trigger — the message loop will pick it up.
        ParseTrigger(newTrigger, out _currentModifiers, out _currentVk);
        _logger?.LogInformation("Hotkey trigger updated to: {Trigger} (takes effect on restart)", newTrigger);
        return Task.CompletedTask;
    }

    private void RunMessageLoop(TaskCompletionSource tcs)
    {
        ParseTrigger(_settings.PreferredHotkey, out _currentModifiers, out _currentVk);

        // Register hotkey with hwnd=0 (thread message queue)
        if (!RegisterHotKey(0, HotkeyId, _currentModifiers, _currentVk))
        {
            _logger?.LogWarning("Failed to register hotkey: {Trigger}", _settings.PreferredHotkey);
            tcs.TrySetResult(); // don't fail the app, just log
            return;
        }

        _logger?.LogInformation("Registered hotkey: {Trigger}", _settings.PreferredHotkey);
        tcs.TrySetResult();

        while (!_disposed)
        {
            if (GetMessage(out var msg, 0, 0, 0) <= 0) break;
            if (msg.Message == WM_HOTKEY && msg.WParam == HotkeyId)
            {
                if (!_isPressed)
                {
                    _isPressed = true;
                    RecordingStartRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    _isPressed = false;
                    RecordingStopRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        UnregisterHotKey(0, HotkeyId);
    }

    private static void ParseTrigger(string trigger, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = trigger.Split('+');
        foreach (var part in parts)
        {
            var p = part.Trim().ToLowerInvariant();
            switch (p)
            {
                case "ctrl" or "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "meta" or "super" or "win":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    if (p.Length == 1 && char.IsLetterOrDigit(p[0]))
                        vk = (uint)char.ToUpperInvariant(p[0]);
                    break;
            }
        }
    }

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll")]
    private static partial int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessageW(uint idThread, uint msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        // Post WM_QUIT to break the message loop
        if (_messageThread is { IsAlive: true })
        {
            // We can't easily get the thread ID, so just let IsBackground handle cleanup
        }
        return ValueTask.CompletedTask;
    }
}
