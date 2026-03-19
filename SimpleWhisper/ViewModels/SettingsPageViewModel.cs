using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SimpleWhisper.Resources;
using SimpleWhisper.Services;
using SimpleWhisper.Services.Hotkey;

namespace SimpleWhisper.ViewModels;

public record ThemeOption(AppTheme Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settings;
    private readonly ILocalizationService _localization;
    private readonly IInputDeviceService _inputDeviceService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IAutoStartService _autoStartService;
    private readonly ILogger<SettingsPageViewModel>? _logger;
    private readonly bool _initialHwAccel;

    [ObservableProperty] private bool _isToggleMode;
    [ObservableProperty] private string _hotkeyText;
    [ObservableProperty] private bool _copyToClipboard;
    [ObservableProperty] private bool _showNotification;
    [ObservableProperty] private bool _pasteIntoFocusedWindow;
    [ObservableProperty] private bool _useHardwareAcceleration;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _autoStart;
    [ObservableProperty] private bool _needsRestart;
    [ObservableProperty] private bool _needsLanguageRestart;
    [ObservableProperty] private string _modelsDirectory;
    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private AudioInputDevice? _selectedInputDevice;
    [ObservableProperty] private ThemeOption? _selectedTheme;

    public ObservableCollection<AudioInputDevice> InputDevices { get; } = [];
    public IReadOnlyList<LanguageOption> AvailableLanguages => _localization.AvailableLanguages;

    public IReadOnlyList<ThemeOption> AvailableThemes { get; } =
    [
        new(AppTheme.System, Strings.SettingsThemeSystem),
        new(AppTheme.Light, Strings.SettingsThemeLight),
        new(AppTheme.Dark, Strings.SettingsThemeDark),
    ];

    public string OsDescription { get; }
    public string DesktopEnvironment { get; }
    public string DisplayServer { get; }
    public bool IsPasteAvailable { get; }
    public bool IsHotkeyEditable { get; }
    public bool IsGpuAvailable { get; }
    public string GpuAccelerationLabel { get; }

    public SettingsPageViewModel(IAppSettingsService settings, ILocalizationService localization, IInputDeviceService inputDeviceService, IGlobalHotkeyService hotkeyService, IAutoStartService autoStartService, ILogger<SettingsPageViewModel>? logger = null)
    {
        _settings = settings;
        _localization = localization;
        _inputDeviceService = inputDeviceService;
        _hotkeyService = hotkeyService;
        _autoStartService = autoStartService;
        _logger = logger;
        _isToggleMode = settings.RecordingMode == RecordingMode.Toggle;
        _hotkeyText = settings.PreferredHotkey;
        _copyToClipboard = settings.CopyToClipboard;
        _showNotification = settings.ShowNotification;
        _pasteIntoFocusedWindow = settings.PasteIntoFocusedWindow;
        _useHardwareAcceleration = settings.UseHardwareAcceleration;
        _minimizeToTray = settings.MinimizeToTray;
        _autoStart = autoStartService.IsEnabled;
        _initialHwAccel = settings.UseHardwareAcceleration;
        _modelsDirectory = settings.ModelsDirectory;
        _selectedLanguage = localization.CurrentLanguage;
        _selectedTheme = AvailableThemes.FirstOrDefault(t => t.Value == settings.Theme) ?? AvailableThemes[0];

        PopulateInputDevices();

        OsDescription = RuntimeInformation.OSDescription;

        if (OperatingSystem.IsWindows())
        {
            DesktopEnvironment = "Windows";
            DisplayServer = "Win32";
            IsPasteAvailable = true;
            IsHotkeyEditable = true;
        }
        else if (OperatingSystem.IsMacOS())
        {
            DesktopEnvironment = "macOS";
            DisplayServer = "Quartz";
            IsPasteAvailable = true;
            IsHotkeyEditable = true;
        }
        else
        {
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
            IsHotkeyEditable = IsPasteAvailable;
        }

        var gpu = GpuDetectionService.Detect();
        IsGpuAvailable = gpu.Backend != GpuBackend.None;
        GpuAccelerationLabel = gpu.Backend switch
        {
            GpuBackend.Cuda => string.Format(Strings.GpuAccelCuda, gpu.Name),
            GpuBackend.Vulkan => string.Format(Strings.GpuAccelVulkan, gpu.Name),
            _ => Strings.GpuAccelUnavailable
        };
    }

    partial void OnIsToggleModeChanged(bool value) =>
        _settings.RecordingMode = value ? RecordingMode.Toggle : RecordingMode.Hold;

    partial void OnCopyToClipboardChanged(bool value) =>
        _settings.CopyToClipboard = value;

    partial void OnShowNotificationChanged(bool value) =>
        _settings.ShowNotification = value;

    partial void OnPasteIntoFocusedWindowChanged(bool value) =>
        _settings.PasteIntoFocusedWindow = value;

    partial void OnUseHardwareAccelerationChanged(bool value)
    {
        _settings.UseHardwareAcceleration = value;
        NeedsRestart = value != _initialHwAccel;
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _settings.MinimizeToTray = value;
        if (Application.Current is App app)
            app.OnMinimizeToTrayChanged(value);
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (value is null) return;
        _settings.Theme = value.Value;
        if (Application.Current is App app)
            app.ApplyTheme(value.Value);
    }

    partial void OnAutoStartChanged(bool value)
    {
        if (value)
            _autoStartService.Enable();
        else
            _autoStartService.Disable();
    }

    partial void OnModelsDirectoryChanged(string value) => _settings.ModelsDirectory = value;

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null) return;
        _localization.SetLanguage(value.Code);
        NeedsLanguageRestart = _localization.NeedsRestart;
    }

    partial void OnSelectedInputDeviceChanged(AudioInputDevice? value) =>
        _settings.SelectedInputDeviceName = value is null || value == AudioInputDevice.SystemDefault
            ? null
            : value.Name;

    private void PopulateInputDevices()
    {
        InputDevices.Clear();
        try
        {
            foreach (var device in _inputDeviceService.GetInputDevices())
                InputDevices.Add(device);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enumerate audio input devices");
            InputDevices.Add(AudioInputDevice.SystemDefault);
        }

        var savedName = _settings.SelectedInputDeviceName;
        SelectedInputDevice = savedName is not null
            ? InputDevices.FirstOrDefault(d => d.Name == savedName) ?? InputDevices[0]
            : InputDevices[0];
    }

    [RelayCommand]
    private void RefreshInputDevices() => PopulateInputDevices();

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
    private void RestartApp()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is not null)
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = false });
            Environment.Exit(0);
        }
    }

    [RelayCommand]
    private void OpenModelsDirectory()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ModelsDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to open models directory");
        }
    }

    [RelayCommand]
    private async Task BrowseModelsDirectoryAsync()
    {
        var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (mainWindow is null) return;

        var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.SettingsSelectModelsDirectory,
            AllowMultiple = false,
            SuggestedStartLocation = await mainWindow.StorageProvider.TryGetFolderFromPathAsync(ModelsDirectory)
        });

        if (result is [var folder])
            ModelsDirectory = folder.Path.LocalPath;
    }

    [RelayCommand]
    private void OpenSystemShortcuts()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("ms-settings:keyboard") { UseShellExecute = true });
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open", "x-apple.systempreferences:com.apple.preference.keyboard") { UseShellExecute = false });
                return;
            }

            var de = DesktopEnvironment.ToUpperInvariant();
            var command = de switch
            {
                _ when de.Contains("KDE") => "systemsettings kcm_keys",
                _ when de.Contains("GNOME") => "gnome-control-center keyboard",
                _ => "xdg-open x-settings://keyboard/shortcuts",
            };
            var parts = command.Split(' ', 2);
            using var proc = Process.Start(new ProcessStartInfo(parts[0], parts.Length > 1 ? parts[1] : "")
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