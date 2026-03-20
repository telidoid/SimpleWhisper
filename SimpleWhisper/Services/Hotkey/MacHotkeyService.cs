using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SimpleWhisper.Services.Hotkey;

public sealed partial class MacHotkeyService : IGlobalHotkeyService
{
    private readonly IAppSettingsService _settings;
    private readonly ILogger<MacHotkeyService>? _logger;
    private nint _eventTap;
    private nint _runLoopSource;
    private Thread? _tapThread;
    private volatile bool _disposed;
    private bool _isPressed;
    private ulong _targetFlags;
    private int _targetKeyCode;

    public event EventHandler? RecordingStartRequested;
    public event EventHandler? RecordingStopRequested;

    public MacHotkeyService(IAppSettingsService settings, ILogger<MacHotkeyService>? logger = null)
    {
        _settings = settings;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        ParseTrigger(_settings.PreferredHotkey, out _targetFlags, out _targetKeyCode);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _tapThread = new Thread(() => RunEventTap(tcs))
        {
            IsBackground = true,
            Name = "MacHotkeyEventTap"
        };
        _tapThread.Start();
        return tcs.Task;
    }

    public Task RebindAsync(string newTrigger, CancellationToken ct = default)
    {
        ParseTrigger(newTrigger, out _targetFlags, out _targetKeyCode);
        _logger?.LogInformation("Rebound hotkey to: {Trigger}", newTrigger);
        return Task.CompletedTask;
    }

    private void RunEventTap(TaskCompletionSource tcs)
    {
        _callback = EventTapCallback;

        _eventTap = CGEventTapCreate(
            0, // kCGHIDEventTap
            0, // kCGHeadInsertEventTap
            1, // kCGEventTapOptionListenOnly
            (1UL << 10) | (1UL << 12), // keyDown | flagsChanged
            _callback,
            nint.Zero);

        if (_eventTap == 0)
        {
            tcs.TrySetException(new InvalidOperationException(
                "Failed to create event tap. Enable Accessibility permissions for SimpleWhisper in System Settings."));
            return;
        }

        _runLoopSource = CFMachPortCreateRunLoopSource(nint.Zero, _eventTap, 0);
        var runLoop = CFRunLoopGetCurrent();
        CFRunLoopAddSource(runLoop, _runLoopSource, CFRunLoopDefaultMode);

        tcs.TrySetResult();

        while (!_disposed)
        {
            CFRunLoopRunInMode(CFRunLoopDefaultMode, 1.0, true);
        }
    }

    private CGEventTapCallBack? _callback;

    private nint EventTapCallback(nint proxy, uint type, nint eventRef, nint userInfo)
    {
        var flags = CGEventGetFlags(eventRef);
        var keyCode = CGEventGetIntegerValueField(eventRef, 9); // kCGKeyboardEventKeycode

        if (keyCode == _targetKeyCode && (flags & _targetFlags) == _targetFlags)
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

        return eventRef;
    }

    private static void ParseTrigger(string trigger, out ulong flags, out int keyCode)
    {
        flags = 0;
        keyCode = 0;

        var parts = trigger.Split('+');
        foreach (var part in parts)
        {
            var p = part.Trim().ToLowerInvariant();
            switch (p)
            {
                case "ctrl" or "control":
                    flags |= 0x40000; // kCGEventFlagMaskControl
                    break;
                case "alt" or "option":
                    flags |= 0x80000; // kCGEventFlagMaskAlternate
                    break;
                case "shift":
                    flags |= 0x20000; // kCGEventFlagMaskShift
                    break;
                case "meta" or "super" or "cmd" or "command":
                    flags |= 0x100000; // kCGEventFlagMaskCommand
                    break;
                default:
                    keyCode = MapKeyToMacKeyCode(p);
                    break;
            }
        }
    }

    private static int MapKeyToMacKeyCode(string key) => key.ToLowerInvariant() switch
    {
        "a" => 0, "s" => 1, "d" => 2, "f" => 3, "h" => 4, "g" => 5,
        "z" => 6, "x" => 7, "c" => 8, "v" => 9, "b" => 11, "q" => 12,
        "w" => 13, "e" => 14, "r" => 15, "y" => 16, "t" => 17,
        "o" => 31, "u" => 32, "i" => 34, "p" => 35, "l" => 37,
        "j" => 38, "k" => 40, "n" => 45, "m" => 46,
        "space" => 49,
        _ => 0,
    };

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint CGEventTapCallBack(nint proxy, uint type, nint eventRef, nint userInfo);

    private static readonly nint CFRunLoopDefaultMode = CFStringCreateWithCString(nint.Zero, "kCFRunLoopDefaultMode", 0);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial nint CGEventTapCreate(
        int tap, int place, int options, ulong eventsOfInterest,
        CGEventTapCallBack callback, nint userInfo);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial ulong CGEventGetFlags(nint eventRef);

    [LibraryImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static partial long CGEventGetIntegerValueField(nint eventRef, int field);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFMachPortCreateRunLoopSource(nint allocator, nint port, int order);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFRunLoopGetCurrent();

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRunLoopAddSource(nint rl, nint source, nint mode);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial int CFRunLoopRunInMode(nint mode, double seconds, [MarshalAs(UnmanagedType.Bool)] bool returnAfterSourceHandled);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint CFStringCreateWithCString(nint allocator, string cStr, int encoding);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRelease(nint cf);

    public ValueTask DisposeAsync()
    {
        _disposed = true;

        if (_runLoopSource != 0)
        {
            CFRelease(_runLoopSource);
            _runLoopSource = 0;
        }

        if (_eventTap != 0)
        {
            CFRelease(_eventTap);
            _eventTap = 0;
        }

        return ValueTask.CompletedTask;
    }
}
