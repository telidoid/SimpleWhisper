using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleWhisper.Services;

namespace SimpleWhisper.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settings;

    [ObservableProperty] private bool _isToggleMode;
    [ObservableProperty] private string _hotkeyText;
    [ObservableProperty] private bool _copyToClipboard;
    [ObservableProperty] private bool _showNotification;
    [ObservableProperty] private bool _pasteIntoFocusedWindow;

    public string OsDescription { get; }
    public string DesktopEnvironment { get; }
    public string DisplayServer { get; }
    public bool IsPasteAvailable { get; }

    public SettingsPageViewModel(IAppSettingsService settings)
    {
        _settings = settings;
        _isToggleMode = settings.RecordingMode == RecordingMode.Toggle;
        _hotkeyText = settings.PreferredHotkey;
        _copyToClipboard = settings.CopyToClipboard;
        _showNotification = settings.ShowNotification;
        _pasteIntoFocusedWindow = settings.PasteIntoFocusedWindow;

        OsDescription = RuntimeInformation.OSDescription;
        DesktopEnvironment = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")
                             ?? Environment.GetEnvironmentVariable("DESKTOP_SESSION")
                             ?? "Unknown";

        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (sessionType != null)
        {
            DisplayServer = sessionType;
        }
        else
        {
            var hasWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
            var hasX11 = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));
            DisplayServer = (hasWayland, hasX11) switch
            {
                (true, true) => "XWayland",
                (true, false) => "Wayland",
                (false, true) => "X11",
                _ => "Unknown",
            };
        }

        IsPasteAvailable = DisplayServer.Equals("x11", StringComparison.OrdinalIgnoreCase);
    }

    partial void OnIsToggleModeChanged(bool value) =>
        _settings.RecordingMode = value ? RecordingMode.Toggle : RecordingMode.Hold;

    partial void OnCopyToClipboardChanged(bool value) =>
        _settings.CopyToClipboard = value;

    partial void OnShowNotificationChanged(bool value) =>
        _settings.ShowNotification = value;

    partial void OnPasteIntoFocusedWindowChanged(bool value) =>
        _settings.PasteIntoFocusedWindow = value;

    [RelayCommand]
    private void SaveHotkey() => _settings.PreferredHotkey = HotkeyText;
}