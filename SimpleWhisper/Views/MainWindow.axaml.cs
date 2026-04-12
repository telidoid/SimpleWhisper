using Avalonia;
using Avalonia.Controls;

namespace SimpleWhisper;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        var app = Application.Current as App;
        if (!App.IsQuitting
            && e.CloseReason == WindowCloseReason.WindowClosing
            && app?.IsTrayEnabled == true)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
