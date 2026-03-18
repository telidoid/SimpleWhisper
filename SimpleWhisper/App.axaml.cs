using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SimpleWhisper.Services;
using SimpleWhisper.ViewModels;

namespace SimpleWhisper;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private IAppSettingsService? _settings;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private Window? _mainWindow;

    public static bool IsQuitting { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            _settings = Program.AppHost.Services.GetRequiredService<IAppSettingsService>();

            _mainWindow = new MainWindow
            {
                DataContext = Program.AppHost.Services.GetRequiredService<MainWindowViewModel>()
            };
            desktop.MainWindow = _mainWindow;

            SetupTrayIcon();
            ApplyTrayMode(_settings.MinimizeToTray);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon()
    {
        var showItem = new NativeMenuItem("Show");
        showItem.Click += (_, _) => ShowMainWindow();

        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => QuitApplication();

        var menu = new NativeMenu();
        menu.Items.Add(showItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quitItem);

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(typeof(App).Assembly.GetManifestResourceStream("SimpleWhisper.Assets.tray-icon.png")
                ?? throw new InvalidOperationException("Could not load tray icon resource")),
            ToolTipText = "SimpleWhisper",
            Menu = menu,
            IsVisible = false
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow();
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
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void QuitApplication()
    {
        IsQuitting = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _desktop?.Shutdown();
    }
}
