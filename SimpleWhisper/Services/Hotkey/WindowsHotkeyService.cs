using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Win32;
using Microsoft.Extensions.Logging;

namespace SimpleWhisper.Services.Hotkey;

public sealed partial class WindowsHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0x5750; // 'WP' for Whisper
    private const int WM_HOTKEY = 0x0312;

    private readonly IAppSettingsService _settings;
    private readonly ILogger<WindowsHotkeyService>? _logger;
    private nint _hwnd;
    private volatile bool _disposed;
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
        ParseTrigger(_settings.PreferredHotkey, out _currentModifiers, out _currentVk);
        _logger?.LogInformation("Hotkey parsed: {Trigger} (waiting for window)", _settings.PreferredHotkey);
        return Task.CompletedTask;
    }

    public void AttachToWindow(Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (handle == 0)
        {
            _logger?.LogWarning("Failed to get window handle for hotkey registration");
            return;
        }

        _hwnd = handle;

        if (!RegisterHotKey(_hwnd, HotkeyId, _currentModifiers | MOD_NOREPEAT, _currentVk))
        {
            _logger?.LogWarning("Failed to register hotkey: {Trigger}", _settings.PreferredHotkey);
            return;
        }

        _logger?.LogInformation("Registered hotkey: {Trigger}", _settings.PreferredHotkey);
        Win32Properties.AddWndProcHookCallback(window, WndProcCallback);
    }

    public Task RebindAsync(string newTrigger, CancellationToken ct = default)
    {
        var wasPolling = _releaseTimer != null;
        StopReleasePolling();

        if (_hwnd != 0)
            UnregisterHotKey(_hwnd, HotkeyId);

        ParseTrigger(newTrigger, out _currentModifiers, out _currentVk);

        if (wasPolling)
            RecordingStopRequested?.Invoke(this, EventArgs.Empty);

        if (_hwnd != 0)
        {
            if (!RegisterHotKey(_hwnd, HotkeyId, _currentModifiers | MOD_NOREPEAT, _currentVk))
                _logger?.LogWarning("Failed to re-register hotkey: {Trigger}", newTrigger);
            else
                _logger?.LogInformation("Hotkey re-registered: {Trigger}", newTrigger);
        }

        return Task.CompletedTask;
    }

    private nint WndProcCallback(nint hwnd, uint msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam == HotkeyId)
        {
            handled = true;
            RecordingStartRequested?.Invoke(this, EventArgs.Empty);
            StartReleasePolling();
        }

        return 0;
    }

    private void StartReleasePolling()
    {
        StopReleasePolling();

        var vk = (int)_currentVk;

        // Handle ultra-fast tap: key already released before polling starts
        if ((GetAsyncKeyState(vk) & 0x8000) == 0)
        {
            RecordingStopRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        _releaseTimer = new Timer(_ =>
        {
            if (_disposed) return;

            if ((GetAsyncKeyState(vk) & 0x8000) == 0)
            {
                StopReleasePolling();
                RecordingStopRequested?.Invoke(this, EventArgs.Empty);
            }
        }, null, 50, 50);
    }

    private void StopReleasePolling()
    {
        var timer = Interlocked.Exchange(ref _releaseTimer, null);
        timer?.Dispose();
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

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            StopReleasePolling();
            if (_hwnd != 0)
            {
                UnregisterHotKey(_hwnd, HotkeyId);
                _hwnd = 0;
            }
        }

        return ValueTask.CompletedTask;
    }
}
