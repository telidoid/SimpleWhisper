namespace SimpleWhisper.Audio.PortAudio.Services;

public interface IInputDeviceService
{
    List<AudioInputDevice> GetInputDevices();
    void ActivateDevice(string? deviceName);
    void RestoreDefaultDevice();
}
