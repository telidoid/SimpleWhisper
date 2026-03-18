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

        if (!RegisterHotKey(_hwnd, HotkeyId, _currentModifiers, _currentVk))
        {
            _logger?.LogWarning("Failed to register hotkey: {Trigger}", _settings.PreferredHotkey);
            return;
        }

        _logger?.LogInformation("Registered hotkey: {Trigger}", _settings.PreferredHotkey);
        Win32Properties.AddWndProcHookCallback(window, WndProcCallback);
    }

    public Task RebindAsync(string newTrigger, CancellationToken ct = default)
    {
        if (_hwnd != 0)
            UnregisterHotKey(_hwnd, HotkeyId);

        ParseTrigger(newTrigger, out _currentModifiers, out _currentVk);
        _isPressed = false;

        if (_hwnd != 0)
        {
            if (!RegisterHotKey(_hwnd, HotkeyId, _currentModifiers, _currentVk))
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

        return 0;
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

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hWnd, int id);

    public ValueTask DisposeAsync()
    {
        if (!_disposed && _hwnd != 0)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _hwnd = 0;
        }

        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
