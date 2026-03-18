namespace SimpleWhisper.Services;

public record AudioInputDevice(int Index, string Name, string? DisplayName = null)
{
    public static AudioInputDevice SystemDefault { get; } = new(-1, "System Default");

    public override string ToString() => DisplayName ?? Name;
}

public interface IInputDeviceService
{
    List<AudioInputDevice> GetInputDevices();
    void ActivateDevice(string? deviceName);
    void RestoreDefaultDevice();
}
