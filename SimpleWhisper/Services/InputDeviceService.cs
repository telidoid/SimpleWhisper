using System.Diagnostics;
using System.Text.Json;
using PortAudioSharp;

namespace SimpleWhisper.Services;

public class InputDeviceService : IInputDeviceService
{
    private string? _previousDefaultSource;

    public List<AudioInputDevice> GetInputDevices()
    {
        var devices = new List<AudioInputDevice> { AudioInputDevice.SystemDefault };

        if (OperatingSystem.IsLinux())
        {
            // On Linux, PortAudio's ALSA backend exposes many virtual plugin
            // devices and raw hw: devices that fail at non-native sample rates.
            // Use pactl to list real audio sources from PipeWire/PulseAudio.
            try
            {
                var json = RunCommand("pactl", "-f json list sources");
                using var doc = JsonDocument.Parse(json);
                foreach (var source in doc.RootElement.EnumerateArray())
                {
                    var name = source.GetProperty("name").GetString()!;
                    var description = source.GetProperty("description").GetString() ?? name;
                    // Skip monitor sources (they capture output audio, not mic input)
                    if (name.Contains(".monitor")) continue;
                    devices.Add(new AudioInputDevice(-1, name, description));
                }
            }
            catch
            {
                // pactl not available — fall back to PortAudio enumeration
                AddPortAudioInputDevices(devices);
            }
        }
        else
        {
            AddPortAudioInputDevices(devices);
        }

        return devices;
    }

    public void ActivateDevice(string? deviceName)
    {
        if (deviceName is null) return;

        if (OperatingSystem.IsLinux())
        {
            // Save the current default source so we can restore it after recording.
            // Then set the selected source as default so PortAudio picks it up.
            try
            {
                _previousDefaultSource = RunCommand("pactl", "get-default-source").Trim();
                RunCommand("pactl", $"set-default-source {deviceName}");

                // Re-initialize PortAudio so it sees the new default source.
                PortAudio.Terminate();
                PortAudio.Initialize();
            }
            catch { /* fall through to default device */ }
        }
    }

    public void RestoreDefaultDevice()
    {
        if (_previousDefaultSource is null) return;
        try { RunCommand("pactl", $"set-default-source {_previousDefaultSource}"); }
        catch { /* ignored */ }
        finally { _previousDefaultSource = null; }
    }

    private static void AddPortAudioInputDevices(List<AudioInputDevice> devices)
    {
        PortAudio.Initialize();
        try
        {
            for (var i = 0; i < PortAudio.DeviceCount; i++)
            {
                var info = PortAudio.GetDeviceInfo(i);
                if (info.maxInputChannels > 0)
                    devices.Add(new AudioInputDevice(i, info.name));
            }
        }
        finally
        {
            PortAudio.Terminate();
        }
    }

    private static string RunCommand(string command, string args)
    {
        using var proc = Process.Start(new ProcessStartInfo(command, args)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        var output = proc!.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return output;
    }
}
