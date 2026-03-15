using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleWhisper.Services;

public partial class AppSettingsService : IAppSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleWhisper", "settings.json");

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

    public bool UseHardwareAcceleration
    {
        get => _data.UseHardwareAcceleration;
        set
        {
            if (_data.UseHardwareAcceleration == value) return;
            _data = _data with { UseHardwareAcceleration = value };
            Save(_data);
        }
    }

    public static string DefaultModelsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleWhisper", "models");

    public string ModelsDirectory
    {
        get => string.IsNullOrEmpty(_data.ModelsDirectory) ? DefaultModelsDirectory : _data.ModelsDirectory;
        set
        {
            var effective = string.IsNullOrEmpty(value) ? DefaultModelsDirectory : value;
            if (_data.ModelsDirectory == effective) return;
            _data = _data with { ModelsDirectory = effective };
            Save(_data);
        }
    }

    private static SettingsData Load()
    {
        if (!File.Exists(SettingsPath)) return new SettingsData();
        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize(json, SettingsSourceGenerationContext.Default.SettingsData) ?? new SettingsData();
        }
        catch
        {
            return new SettingsData();
        }
    }

    private static void Save(SettingsData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, SettingsSourceGenerationContext.Default.SettingsData));
    }

    private record SettingsData
    {
        [JsonConverter(typeof(JsonStringEnumConverter<RecordingMode>))]
        public RecordingMode RecordingMode { get; init; } = RecordingMode.Hold;
        public string PreferredHotkey { get; init; } = "Meta+Alt+W";
        public bool CopyToClipboard { get; init; } = true;
        public bool ShowNotification { get; init; } = true;
        public bool PasteIntoFocusedWindow { get; init; } = false;
        public bool UseHardwareAcceleration { get; init; } = true;
        public string ModelsDirectory { get; init; } = string.Empty;
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(SettingsData))]
    private partial class SettingsSourceGenerationContext : JsonSerializerContext;
}