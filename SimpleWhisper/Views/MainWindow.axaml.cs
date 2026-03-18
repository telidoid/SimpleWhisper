using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SimpleWhisper.Services.Hotkey;

namespace SimpleWhisper;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (OperatingSystem.IsWindows())
        {
            Loaded += OnLoadedWindows;
        }
    }

    private void OnLoadedWindows(object? sender, EventArgs e)
    {
        Loaded -= OnLoadedWindows;
        var hotkeyService = Program.AppHost.Services.GetRequiredService<IGlobalHotkeyService>();
        if (hotkeyService is WindowsHotkeyService winService)
        {
            winService.AttachToWindow(this);
        }
    }
}
