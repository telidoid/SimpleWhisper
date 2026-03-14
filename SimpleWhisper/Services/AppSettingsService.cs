using System.Text.Json;

namespace SimpleWhisper.Services;

public class AppSettingsService : IAppSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleWhisper", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private SettingsData _data = Load();

    public RecordingMode RecordingMode
    {
        get => _data.RecordingMode;
        set
        {
            if (_data.RecordingMode == value) return;
            _data = _data with { RecordingMode = value };
            Save(_data);
        }
    }

    public string PreferredHotkey
    {
        get => _data.PreferredHotkey;
        set
        {
            if (_data.PreferredHotkey == value) return;
            _data = _data with { PreferredHotkey = value };
            Save(_data);
        }
    }

    public bool CopyToClipboard
    {
        get => _data.CopyToClipboard;
        set
        {
            if (_data.CopyToClipboard == value) return;
            _data = _data with { CopyToClipboard = value };
            Save(_data);
        }
    }

    public bool ShowNotification
    {
        get => _data.ShowNotification;
        set
        {
            if (_data.ShowNotification == value) return;
            _data = _data with { ShowNotification = value };
            Save(_data);
        }
    }

    public bool PasteIntoFocusedWindow
    {
        get => _data.PasteIntoFocusedWindow;
        set
        {
            if (_data.PasteIntoFocusedWindow == value) return;
            _data = _data with { PasteIntoFocusedWindow = value };
            Save(_data);
        }
    }

    private static SettingsData Load()
    {
        if (!File.Exists(SettingsPath)) return new SettingsData();
        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
        }
        catch
        {
            return new SettingsData();
        }
    }

    private static void Save(SettingsData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, JsonOptions));
    }

    private record SettingsData
    {
        public RecordingMode RecordingMode { get; init; } = RecordingMode.Hold;
        public string PreferredHotkey { get; init; } = "Meta+Alt+W";
        public bool CopyToClipboard { get; init; } = true;
        public bool ShowNotification { get; init; } = true;
        public bool PasteIntoFocusedWindow { get; init; } = false;
    }
}