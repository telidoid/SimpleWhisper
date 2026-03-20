using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SimpleWhisper.Services.Hotkey;

/// <summary>
/// Push-to-talk global hotkey for X11 Linux using XGrabKey.
/// Uses a dedicated X11 display connection and a background thread
/// blocked on XNextEvent (zero CPU usage while idle).
/// </summary>
public sealed partial class X11HotkeyService : IGlobalHotkeyService
{
    private const int KeyPress = 2;
    private const int KeyRelease = 3;
    private const int ClientMessage = 33;
    private const int GrabModeAsync = 1;

    // X11 modifier masks
    private const uint ShiftMask = 1 << 0;
    private const uint LockMask = 1 << 1;   // CapsLock
    private const uint ControlMask = 1 << 2;
    private const uint Mod1Mask = 1 << 3;    // Alt
    private const uint Mod2Mask = 1 << 4;    // NumLock
    private const uint Mod4Mask = 1 << 6;    // Super/Meta

    // Lock masks to grab through (NumLock, CapsLock, both)
    private static readonly uint[] LockMasks = [0, Mod2Mask, LockMask, Mod2Mask | LockMask];

    private readonly IAppSettingsService _settings;
    private readonly ILogger<X11HotkeyService>? _logger;
    private readonly Lock _grabLock = new();
    private nint _display;
    private nint _rootWindow;
    private nint _signalWindow;
    private Thread? _eventThread;
    private volatile bool _disposed;
    private uint _modifiers;
    private int _keycode;

    public event EventHandler? RecordingStartRequested;
    public event EventHandler? RecordingStopRequested;

    public X11HotkeyService(IAppSettingsService settings, ILogger<X11HotkeyService>? logger = null)
    {
        _settings = settings;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        XInitThreads();

        _display = XOpenDisplay(nint.Zero);
        if (_display == nint.Zero)
            throw new InvalidOperationException("Failed to open X11 display. Is DISPLAY set?");

        _rootWindow = XDefaultRootWindow(_display);

        // Suppress fake KeyRelease/KeyPress pairs during key auto-repeat.
        // With detectable auto-repeat, we get one KeyPress on press and one KeyRelease on actual release.
        XkbSetDetectableAutoRepeat(_display, true, out _);

        // Create a small unmapped window for signaling shutdown.
        // XSendEvent with event_mask=0 delivers to the client that created the window.
        _signalWindow = XCreateSimpleWindow(_display, _rootWindow, 0, 0, 1, 1, 0, 0, 0);

        ParseTrigger(_settings.PreferredHotkey, out _modifiers, out var keysym);
        _keycode = XKeysymToKeycode(_display, keysym);

        if (_keycode == 0)
        {
            _logger?.LogWarning("Failed to map keysym {Keysym} to keycode for trigger: {Trigger}",
                keysym, _settings.PreferredHotkey);
        }
        else if (!GrabKey(_modifiers, _keycode))
        {
            _logger?.LogWarning("Failed to grab hotkey (already grabbed by another app?): {Trigger}",
                _settings.PreferredHotkey);
        }
        else
        {
            _logger?.LogInformation("X11 hotkey grabbed: {Trigger}", _settings.PreferredHotkey);
        }

        var tcs = new TaskCompletionSource();
        _eventThread = new Thread(() => EventLoop(tcs))
        {
            IsBackground = true,
            Name = "X11HotkeyEventLoop"
        };
        _eventThread.Start();
        return tcs.Task;
    }

    public Task RebindAsync(string newTrigger, CancellationToken ct = default)
    {
        if (_display == nint.Zero) return Task.CompletedTask;

        lock (_grabLock)
        {
            UngrabKey(_modifiers, _keycode);

            ParseTrigger(newTrigger, out _modifiers, out var keysym);
            _keycode = XKeysymToKeycode(_display, keysym);

            if (_keycode == 0)
            {
                _logger?.LogWarning("Failed to map keysym for rebind: {Trigger}", newTrigger);
            }
            else if (!GrabKey(_modifiers, _keycode))
            {
                _logger?.LogWarning("Failed to grab rebound hotkey (already grabbed?): {Trigger}", newTrigger);
            }
            else
            {
                _logger?.LogInformation("X11 hotkey rebound: {Trigger}", newTrigger);
            }
        }

        return Task.CompletedTask;
    }

    private unsafe bool GrabKey(uint modifiers, int keycode)
    {
        // Install a temporary error handler to catch BadAccess (key already grabbed by another app)
        _grabFailed = false;
        var oldHandler = XSetErrorHandler(&GrabErrorHandler);

        foreach (var lockMask in LockMasks)
        {
            XGrabKey(_display, keycode, modifiers | lockMask, _rootWindow,
                0, GrabModeAsync, GrabModeAsync);
        }

        XSync(_display, false);
        XSetErrorHandler(oldHandler);

        return !_grabFailed;
    }

    [ThreadStatic] private static bool _grabFailed;

    [UnmanagedCallersOnly]
    private static int GrabErrorHandler(nint display, nint errorEvent)
    {
        _grabFailed = true;
        return 0;
    }

    private void UngrabKey(uint modifiers, int keycode)
    {
        foreach (var lockMask in LockMasks)
        {
            XUngrabKey(_display, keycode, modifiers | lockMask, _rootWindow);
        }

        XSync(_display, false);
    }

    private void EventLoop(TaskCompletionSource tcs)
    {
        tcs.TrySetResult();

        Span<byte> eventBuffer = stackalloc byte[192]; // XEvent union is 192 bytes on 64-bit
        ref var xEvent = ref MemoryMarshal.AsRef<XEventHeader>(eventBuffer);

        while (!_disposed)
        {
            XNextEvent(_display, ref MemoryMarshal.GetReference(eventBuffer));

            uint currentMods;
            int currentKeycode;
            lock (_grabLock)
            {
                currentMods = _modifiers;
                currentKeycode = _keycode;
            }

            // Strip lock modifier bits for comparison
            var eventMods = xEvent.State & ~(LockMask | Mod2Mask);

            switch (xEvent.Type)
            {
                case KeyPress when xEvent.Keycode == currentKeycode && eventMods == currentMods:
                    RecordingStartRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case KeyRelease when xEvent.Keycode == currentKeycode && eventMods == currentMods:
                    RecordingStopRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case ClientMessage:
                    // Dummy event sent by DisposeAsync to unblock XNextEvent
                    break;
            }
        }
    }

    private static void ParseTrigger(string trigger, out uint modifiers, out nint keysym)
    {
        modifiers = 0;
        keysym = 0;

        var parts = trigger.Split('+');
        foreach (var part in parts)
        {
            var p = part.Trim().ToLowerInvariant();
            switch (p)
            {
                case "ctrl" or "control":
                    modifiers |= ControlMask;
                    break;
                case "alt":
                    modifiers |= Mod1Mask;
                    break;
                case "shift":
                    modifiers |= ShiftMask;
                    break;
                case "meta" or "super" or "win":
                    modifiers |= Mod4Mask;
                    break;
                default:
                    keysym = MapKeyNameToKeysym(p);
                    break;
            }
        }
    }

    private static nint MapKeyNameToKeysym(string key)
    {
        // XStringToKeysym handles standard names like "w", "F1", "space", etc.
        // but needs exact X11 names, so map common aliases first.
        var x11Name = key switch
        {
            "enter" => "Return",
            "backspace" or "back" => "BackSpace",
            "delete" => "Delete",
            "insert" => "Insert",
            "home" => "Home",
            "end" => "End",
            "pageup" or "prior" => "Prior",
            "pagedown" or "next" => "Next",
            "escape" => "Escape",
            "tab" => "Tab",
            "space" => "space",
            "left" => "Left",
            "up" => "Up",
            "right" => "Right",
            "down" => "Down",
            "f1" => "F1", "f2" => "F2", "f3" => "F3", "f4" => "F4",
            "f5" => "F5", "f6" => "F6", "f7" => "F7", "f8" => "F8",
            "f9" => "F9", "f10" => "F10", "f11" => "F11", "f12" => "F12",
            _ => key // Single letter keys work as-is with XStringToKeysym
        };

        return XStringToKeysym(x11Name);
    }

    // XEvent header — first fields are shared across all event types.
    // For KeyPress/KeyRelease (XKeyEvent), the layout is:
    //   int type, unsigned long serial, Bool send_event, Display* display,
    //   Window window, Window root, Window subwindow, Time time,
    //   int x, int y, int x_root, int y_root, unsigned int state, unsigned int keycode, ...
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XEventHeader
    {
        [FieldOffset(0)] public int Type;
        // On 64-bit Linux: state is at offset 80, keycode at offset 84
        [FieldOffset(80)] public uint State;
        [FieldOffset(84)] public int Keycode;
    }

    // XClientMessageEvent for unblocking XNextEvent
    // Layout: type(0) serial(8) send_event(16) display(24) window(32) message_type(40) format(48)
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XClientMessageEvent
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(8)] public nuint Serial;
        [FieldOffset(16)] public int SendEvent;
        [FieldOffset(24)] public nint Display;
        [FieldOffset(32)] public nint Window;
        [FieldOffset(40)] public nint MessageType;
        [FieldOffset(48)] public int Format;
    }

    [LibraryImport("libX11.so.6")]
    private static partial int XInitThreads();

    [LibraryImport("libX11.so.6")]
    private static partial nint XOpenDisplay(nint displayName);

    [LibraryImport("libX11.so.6")]
    private static partial int XCloseDisplay(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial nint XDefaultRootWindow(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial int XGrabKey(nint display, int keycode, uint modifiers,
        nint grabWindow, int ownerEvents, int pointerMode, int keyboardMode);

    [LibraryImport("libX11.so.6")]
    private static partial int XUngrabKey(nint display, int keycode, uint modifiers, nint grabWindow);

    [LibraryImport("libX11.so.6")]
    private static partial int XKeysymToKeycode(nint display, nint keysym);

    [LibraryImport("libX11.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint XStringToKeysym(string str);

    [LibraryImport("libX11.so.6")]
    private static partial int XNextEvent(nint display, ref byte eventReturn);

    [LibraryImport("libX11.so.6")]
    private static partial int XSync(nint display, [MarshalAs(UnmanagedType.Bool)] bool discard);

    [LibraryImport("libX11.so.6")]
    private static partial int XSendEvent(nint display, nint window, [MarshalAs(UnmanagedType.Bool)] bool propagate,
        nint eventMask, ref byte eventSend);

    [LibraryImport("libX11.so.6")]
    private static partial int XFlush(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial nint XCreateSimpleWindow(nint display, nint parent,
        int x, int y, uint width, uint height, uint borderWidth, nint border, nint background);

    [LibraryImport("libX11.so.6")]
    private static partial int XDestroyWindow(nint display, nint window);

    [LibraryImport("libX11.so.6")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool XkbSetDetectableAutoRepeat(nint display,
        [MarshalAs(UnmanagedType.Bool)] bool detectable,
        [MarshalAs(UnmanagedType.Bool)] out bool supported);

    [LibraryImport("libX11.so.6")]
    private static unsafe partial nint XSetErrorHandler(delegate* unmanaged<nint, nint, int> handler);

    [LibraryImport("libX11.so.6")]
    private static partial nint XSetErrorHandler(nint handler);

    public ValueTask DisposeAsync()
    {
        if (_disposed || _display == nint.Zero) return ValueTask.CompletedTask;
        _disposed = true;

        UngrabKey(_modifiers, _keycode);

        // Send a dummy ClientMessage to our signal window to unblock XNextEvent.
        // event_mask=0 delivers to the client that created the window (us).
        var clientEvent = new XClientMessageEvent
        {
            Type = ClientMessage,
            Serial = 0,
            SendEvent = 1,
            Display = _display,
            Window = _signalWindow,
            Format = 32
        };
        var eventBytes = MemoryMarshal.AsBytes(new Span<XClientMessageEvent>(ref clientEvent));
        XSendEvent(_display, _signalWindow, false, 0, ref MemoryMarshal.GetReference(eventBytes));
        XFlush(_display);

        _eventThread?.Join(TimeSpan.FromSeconds(2));

        XDestroyWindow(_display, _signalWindow);
        XCloseDisplay(_display);
        _display = nint.Zero;

        return ValueTask.CompletedTask;
    }
}
