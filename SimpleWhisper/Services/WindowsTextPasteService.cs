using System.Runtime.InteropServices;

namespace SimpleWhisper.Services;

public sealed partial class WindowsTextPasteService : ITextPasteService
{
    private readonly IClipboardService _clipboardService;
    private nint _targetWindow;

    public WindowsTextPasteService(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public bool IsAvailable => true;

    public void CaptureTargetWindow()
    {
        _targetWindow = GetForegroundWindow();
    }

    public async Task PasteAsync(string text, CancellationToken ct = default)
    {
        await _clipboardService.SetTextAsync(text);

        var target = _targetWindow;
        if (target == 0) return;

        await Task.Run(() =>
        {
            // Attach threads only for SetForegroundWindow, then detach
            // before sending keystrokes to avoid shared keyboard state interference
            var currentThreadId = GetCurrentThreadId();
            var targetThreadId = GetWindowThreadProcessId(target, out _);
            var attached = currentThreadId != targetThreadId
                && AttachThreadInput(currentThreadId, targetThreadId, true);

            SetForegroundWindow(target);

            if (attached)
                AttachThreadInput(currentThreadId, targetThreadId, false);

            Thread.Sleep(100);

            var inputs = new Input[4];
            inputs[0] = CreateKeyInput(VK_CONTROL, 0);
            inputs[1] = CreateKeyInput(VK_V, 0);
            inputs[2] = CreateKeyInput(VK_V, KEYEVENTF_KEYUP);
            inputs[3] = CreateKeyInput(VK_CONTROL, KEYEVENTF_KEYUP);
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        }, ct);
    }

    private static Input CreateKeyInput(ushort vk, uint flags) => new()
    {
        Type = INPUT_KEYBOARD,
        Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = vk, Flags = flags } }
    };

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    // The Input struct must match the Win32 INPUT struct exactly.
    // The union must be sized to the largest member (MOUSEINPUT) for correct marshaling.
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}
