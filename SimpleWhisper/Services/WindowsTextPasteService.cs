using System.Runtime.InteropServices;

namespace SimpleWhisper.Services;

public sealed partial class WindowsTextPasteService : ITextPasteService
{
    public bool IsAvailable => true;

    public async Task PasteAsync(string text, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            var inputs = new Input[4];

            // Ctrl down
            inputs[0] = CreateKeyInput(VK_CONTROL, 0);
            // V down
            inputs[1] = CreateKeyInput(VK_V, 0);
            // V up
            inputs[2] = CreateKeyInput(VK_V, KEYEVENTF_KEYUP);
            // Ctrl up
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

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
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
