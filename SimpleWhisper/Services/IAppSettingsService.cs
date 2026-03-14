namespace SimpleWhisper.Services;

public enum RecordingMode
{
    Hold,
    Toggle
}

public interface IAppSettingsService
{
    RecordingMode RecordingMode { get; set; }
    string PreferredHotkey { get; set; }
    bool CopyToClipboard { get; set; }
    bool ShowNotification { get; set; }
    bool PasteIntoFocusedWindow { get; set; }
}