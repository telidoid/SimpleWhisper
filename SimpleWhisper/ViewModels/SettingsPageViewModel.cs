using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SimpleWhisper.Services;
using SimpleWhisper.Services.Hotkey;

namespace SimpleWhisper.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settings;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ILogger<SettingsPageViewModel>? _logger;

    [ObservableProperty] private bool _isToggleMode;
    [ObservableProperty] private string _hotkeyText;
    [ObservableProperty] private bool _copyToClipboard;
    [ObservableProperty] private bool _showNotification;
    [ObservableProperty] private bool _pasteIntoFocusedWindow;

    public string OsDescription { get; }
    public string DesktopEnvironment { get; }
    public string DisplayServer { get; }
    public bool IsPasteAvailable { get; }
    public bool IsHotkeyEditable { get; }

    public SettingsPageViewModel(IAppSettingsService settings, IGlobalHotkeyService hotkeyService, ILogger<SettingsPageViewModel>? logger = null)
    {
        _settings = settings;
        _hotkeyService = hotkeyService;
        _logger = logger;
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
        IsHotkeyEditable = IsPasteAvailable; // only X11 allows direct hotkey editing; Wayland/XWayland use the compositor
    }

    partial void OnIsToggleModeChanged(bool value) =>
        _settings.RecordingMode = value ? RecordingMode.Toggle : RecordingMode.Hold;

    partial void OnCopyToClipboardChanged(bool value) =>
        _settings.CopyToClipboard = value;

    partial void OnShowNotificationChanged(bool value) =>
        _settings.ShowNotification = value;

    partial void OnPasteIntoFocusedWindowChanged(bool value) =>
        _settings.PasteIntoFocusedWindow = value;

    partial void OnHotkeyTextChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        _settings.PreferredHotkey = value;
        _ = RebindHotkeyAsync(value);
    }

    private async Task RebindHotkeyAsync(string trigger)
    {
        try
        {
            await _hotkeyService.RebindAsync(trigger);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to rebind hotkey to {Trigger}", trigger);
        }
    }

    [RelayCommand]
    private void OpenSystemShortcuts()
    {
        var de = DesktopEnvironment.ToUpperInvariant();
        var command = de switch
        {
            _ when de.Contains("KDE") => "systemsettings kcm_keys",
            _ when de.Contains("GNOME") => "gnome-control-center keyboard",
            _ => "xdg-open x-settings://keyboard/shortcuts",
        };
        var parts = command.Split(' ', 2);
        try
        {
            Process.Start(new ProcessStartInfo(parts[0], parts.Length > 1 ? parts[1] : "")
            {
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to open system shortcuts settings");
        }
    }
}