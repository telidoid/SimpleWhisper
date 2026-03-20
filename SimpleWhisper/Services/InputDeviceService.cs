using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using PortAudioSharp;

namespace SimpleWhisper.Services;

public partial class InputDeviceService : IInputDeviceService
{
    private const int PaWasapi = 13;

    private string? _previousDefaultSource;

    public List<AudioInputDevice> GetInputDevices()
    {
        var devices = new List<AudioInputDevice> { AudioInputDevice.SystemDefault };

        if (OperatingSystem.IsLinux())
        {
            // On Linux, PortAudio's ALSA backend exposes many virtual plugin
            // devices and raw hw: devices that fail at non-native sample rates.
            // Use pactl to list real audio sources from PipeWire/PulseAudio.
            if (!TryAddPactlSources(devices))
                AddPortAudioInputDevices(devices);
        }
        else if (OperatingSystem.IsWindows())
        {
            // On Windows, PortAudio enumerates devices from multiple host APIs
            // (MME, DirectSound, WASAPI, WDM-KS), causing duplicates.
            // Filter to WASAPI only — matches what Windows Sound Settings shows.
            AddPortAudioInputDevices(devices, hostApiType: PaWasapi);
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
                _previousDefaultSource = RunCommand("pactl", ["get-default-source"]).Trim();
                RunCommand("pactl", ["set-default-source", deviceName]);

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
        try { RunCommand("pactl", ["set-default-source", _previousDefaultSource]); }
        catch { /* ignored */ }
        finally { _previousDefaultSource = null; }
    }

    private static bool TryAddPactlSources(List<AudioInputDevice> devices)
    {
        try
        {
            var json = RunCommand("pactl", ["-f", "json", "list", "sources"]);
            using var doc = JsonDocument.Parse(json);
            var sources = new List<AudioInputDevice>();
            foreach (var source in doc.RootElement.EnumerateArray())
            {
                var name = source.GetProperty("name").GetString();
                if (name is null) continue;
                var description = source.GetProperty("description").GetString() ?? name;
                if (name.Contains(".monitor")) continue;
                sources.Add(new AudioInputDevice(-1, name, description));
            }

            devices.AddRange(sources);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void AddPortAudioInputDevices(List<AudioInputDevice> devices, int? hostApiType = null)
    {
        PortAudio.Initialize();
        try
        {
            for (var i = 0; i < PortAudio.DeviceCount; i++)
            {
                var info = PortAudio.GetDeviceInfo(i);
                if (info.maxInputChannels <= 0) continue;
                if (hostApiType.HasValue && !IsHostApiType(info.hostApi, hostApiType.Value)) continue;
                devices.Add(new AudioInputDevice(i, info.name));
            }
        }
        finally
        {
            PortAudio.Terminate();
        }
    }

    private static unsafe bool IsHostApiType(int hostApiIndex, int expectedType)
    {
        var ptr = Pa_GetHostApiInfo(hostApiIndex);
        if (ptr == nint.Zero) return false;
        // First field is structVersion (int), second is type (int)
        return ((int*)ptr)[1] == expectedType;
    }

    [LibraryImport("portaudio")]
    private static partial nint Pa_GetHostApiInfo(int hostApi);

    private static string RunCommand(string command, string[] args)
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
