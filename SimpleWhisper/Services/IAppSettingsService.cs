namespace SimpleWhisper.Services;

public enum RecordingMode
{
    Hold,
    Toggle
}

public enum AppTheme
{
    System,
    Light,
    Dark
}

public interface IAppSettingsService
{
    RecordingMode RecordingMode { get; set; }
    string PreferredHotkey { get; set; }
    bool CopyToClipboard { get; set; }
    bool ShowNotification { get; set; }
    bool PasteIntoFocusedWindow { get; set; }
    bool UseHardwareAcceleration { get; set; }
    bool MinimizeToTray { get; set; }
    string ModelsDirectory { get; set; }
    string? SelectedInputDeviceName { get; set; }
    string? Language { get; set; }
    AppTheme Theme { get; set; }
}