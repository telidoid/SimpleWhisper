using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SimpleWhisper.Services.Hotkey;

public sealed partial class WindowsHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0x5750;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_QUIT = 0x0012;
    private const uint WM_APP_REBIND = 0x8000;

    private readonly IAppSettingsService _settings;
    private readonly ILogger<WindowsHotkeyService>? _logger;
    private readonly Lock _hotkeyLock = new();
    private Thread? _messageThread;
    private volatile uint _messageThreadId;
    private TaskCompletionSource? _ready;
    private volatile bool _disposed;
    private volatile bool _pressed;
    private Timer? _releaseTimer;
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
        lock (_hotkeyLock)
            ParseTrigger(_settings.PreferredHotkey, out _currentModifiers, out _currentVk);

        _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _messageThread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "WindowsHotkeyMessagePump",
        };
        _messageThread.Start();
        return _ready.Task;
    }

    public Task RebindAsync(string newTrigger, CancellationToken ct = default)
    {
        lock (_hotkeyLock)
            ParseTrigger(newTrigger, out _currentModifiers, out _currentVk);

        StopReleasePolling();

        var tid = _messageThreadId;
        if (tid != 0)
            _ = PostThreadMessage(tid, WM_APP_REBIND, nint.Zero, nint.Zero);

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        StopReleasePolling();

        var tid = _messageThreadId;
        if (tid != 0)
            _ = PostThreadMessage(tid, WM_QUIT, nint.Zero, nint.Zero);

        _messageThread?.Join(TimeSpan.FromSeconds(2));
        return ValueTask.CompletedTask;
    }

    // Thread-associated hotkeys (RegisterHotKey with hWnd == NULL) deliver WM_HOTKEY
    // straight to the owning thread's message queue. All register/unregister calls
    // must happen on this same thread.
    private void MessageLoop()
    {
        _messageThreadId = GetCurrentThreadId();
        RegisterCurrent();
        _ready?.TrySetResult();

        while (!_disposed)
        {
            var ret = GetMessage(out var msg, nint.Zero, 0, 0);
            if (ret <= 0) break;

            switch (msg.message)
            {
                case WM_HOTKEY when (int)msg.wParam == HotkeyId:
                    OnHotkeyPressed();
                    break;
                case WM_APP_REBIND:
                    Rebind();
                    break;
            }
        }

        _ = UnregisterHotKey(nint.Zero, HotkeyId);
    }

    private void RegisterCurrent()
    {
        uint mods, vk;
        lock (_hotkeyLock)
        {
            mods = _currentModifiers;
            vk = _currentVk;
        }

        if (vk == 0)
        {
            _logger?.LogWarning("Unknown hotkey, skipping registration: {Trigger}", _settings.PreferredHotkey);
            return;
        }

        if (!RegisterHotKey(nint.Zero, HotkeyId, mods | MOD_NOREPEAT, vk))
            _logger?.LogWarning("Failed to register hotkey: {Trigger}", _settings.PreferredHotkey);
        else
            _logger?.LogInformation("Registered hotkey: {Trigger}", _settings.PreferredHotkey);
    }

    private void Rebind()
    {
        _ = UnregisterHotKey(nint.Zero, HotkeyId);
        FireStopIfPressed();
        RegisterCurrent();
    }

    private void OnHotkeyPressed()
    {
        _pressed = true;
        RecordingStartRequested?.Invoke(this, EventArgs.Empty);
        StartReleasePolling();
    }

    private void FireStopIfPressed()
    {
        if (!_pressed) return;
        _pressed = false;
        RecordingStopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StartReleasePolling()
    {
        StopReleasePolling();

        int vk;
        lock (_hotkeyLock)
            vk = (int)_currentVk;

        if (IsKeyReleased(vk))
        {
            FireStopIfPressed();
            return;
        }

        _releaseTimer = new Timer(_ => PollKeyRelease(vk), null, 50, 50);
    }

    private void PollKeyRelease(int vk)
    {
        if (_disposed || !_pressed) return;

        if (IsKeyReleased(vk))
        {
            _pressed = false;
            StopReleasePolling();
            RecordingStopRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StopReleasePolling()
    {
        var timer = Interlocked.Exchange(ref _releaseTimer, null);
        timer?.Dispose();
    }

    private static bool IsKeyReleased(int vk) => (GetAsyncKeyState(vk) & 0x8000) == 0;

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
                    vk = MapKeyNameToVk(p);
                    break;
            }
        }
    }

    private static uint MapKeyNameToVk(string key) => key switch
    {
        "space" => 0x20,
        "tab" => 0x09,
        "return" or "enter" => 0x0D,
        "back" or "backspace" => 0x08,
        "delete" => 0x2E,
        "insert" => 0x2D,
        "home" => 0x24,
        "end" => 0x23,
        "pageup" or "prior" => 0x21,
        "pagedown" or "next" => 0x22,
        "escape" => 0x1B,
        "left" => 0x25,
        "up" => 0x26,
        "right" => 0x27,
        "down" => 0x28,
        "f1" => 0x70, "f2" => 0x71, "f3" => 0x72, "f4" => 0x73,
        "f5" => 0x74, "f6" => 0x75, "f7" => 0x76, "f8" => 0x77,
        "f9" => 0x78, "f10" => 0x79, "f11" => 0x7A, "f12" => 0x7B,
        "xbutton1" => 0x05,
        "xbutton2" => 0x06,
        "middlebutton" => 0x04,
        "rightbutton" => 0x02,
        _ => key.Length == 1 && char.IsLetterOrDigit(key[0]) ? (uint)char.ToUpperInvariant(key[0]) : 0,
    };

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    private static partial int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessage(uint idThread, uint msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }
}
