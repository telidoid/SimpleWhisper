using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using SimpleWhisper.Core.Services;
using SimpleWhisper.Resources;
using SimpleWhisper.Services;
using SimpleWhisper.ViewModels;

namespace SimpleWhisper;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private WindowIcon? _idleIcon;
    private WindowIcon? _recordingIcon;
    private WindowIcon? _transcribingIcon;
    private IAppSettingsService? _settings;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private Window? _mainWindow;

    public static bool IsQuitting { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        var settings = Program.AppHost.Services.GetRequiredService<IAppSettingsService>();
        ApplyTheme(settings.Theme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            _settings = Program.AppHost.Services.GetRequiredService<IAppSettingsService>();

            SetupTrayIcon();

            if (Program.StartMinimized && _settings.MinimizeToTray)
            {
                // Defer the entire window creation so the native X11 window
                // handle is never allocated — the only reliable way to start
                // hidden on Linux (X11 maps windows on construction).
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                if (_trayIcon is not null) _trayIcon.IsVisible = true;
            }
            else
            {
                EnsureMainWindow();
                desktop.MainWindow = _mainWindow;
                ApplyTrayMode(_settings.MinimizeToTray);
            }

            var mainPageVm = Program.AppHost.Services.GetRequiredService<MainPageViewModel>();
            mainPageVm.PropertyChanged += OnMainPagePropertyChanged;

            // Pre-warm the model catalog cache in the background
            Program.AppHost.Services.GetRequiredService<IModelCatalogService>().GetAvailableModelsAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void EnsureMainWindow()
    {
        if (_mainWindow is not null) return;

        _mainWindow = new MainWindow
        {
            DataContext = Program.AppHost.Services.GetRequiredService<MainWindowViewModel>()
        };
        _mainWindow.Icon = _idleIcon;
    }

    private void SetupTrayIcon()
    {
        _idleIcon = LoadEmbeddedIcon("SimpleWhisper.Assets.tray-icon.png");
        _recordingIcon = LoadEmbeddedIcon("SimpleWhisper.Assets.tray-icon-recording.png");
        _transcribingIcon = LoadEmbeddedIcon("SimpleWhisper.Assets.tray-icon-transcribing.png");

        var showItem = new NativeMenuItem(Strings.TrayShow);
        showItem.Click += (_, _) => ShowMainWindow();

        var quitItem = new NativeMenuItem(Strings.TrayQuit);
        quitItem.Click += (_, _) => QuitApplication();

        var menu = new NativeMenu();
        menu.Items.Add(showItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quitItem);

        _trayIcon = new TrayIcon
        {
            Icon = _idleIcon,
            ToolTipText = Strings.TrayTooltip,
            Menu = menu,
            IsVisible = false
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow();
    }

    private static WindowIcon LoadEmbeddedIcon(string resourceName)
    {
        var stream = typeof(App).Assembly.GetManifestResourceStream(resourceName)
                     ?? throw new InvalidOperationException($"Could not load embedded resource: {resourceName}");
        return new WindowIcon(stream);
    }

    private void OnMainPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_trayIcon is null || sender is not MainPageViewModel vm) return;
        if (e.PropertyName != nameof(MainPageViewModel.AppState)) return;

        (_trayIcon.Icon, _trayIcon.ToolTipText) = vm.AppState switch
        {
            AppState.Recording => (_recordingIcon, Strings.TrayTooltipRecording),
            AppState.Transcribing => (_transcribingIcon, Strings.TrayTooltipTranscribing),
            _ => (_idleIcon, Strings.TrayTooltip)
        };
    }

    private void ApplyTrayMode(bool enabled)
    {
        if (_desktop is null || _trayIcon is null) return;

        _desktop.ShutdownMode = enabled
            ? ShutdownMode.OnExplicitShutdown
            : ShutdownMode.OnLastWindowClose;

        _trayIcon.IsVisible = enabled;
    }

    public void OnMinimizeToTrayChanged(bool enabled)
    {
        ApplyTrayMode(enabled);

        // If tray mode was just disabled and window is hidden, show it
        if (!enabled && _mainWindow is { IsVisible: false })
            ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (_desktop is null) return;

        EnsureMainWindow();

        // Assign MainWindow so the lifetime tracks it.
        if (_desktop.MainWindow is null)
            _desktop.MainWindow = _mainWindow;

        _mainWindow!.ShowInTaskbar = true;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void ApplyTheme(AppTheme theme)
    {
        RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private void QuitApplication()
    {
        IsQuitting = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _desktop?.Shutdown();
    }
}
