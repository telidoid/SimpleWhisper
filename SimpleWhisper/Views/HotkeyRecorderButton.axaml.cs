using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SimpleWhisper.Views;

public partial class HotkeyRecorderButton : UserControl
{
    public static readonly StyledProperty<string> HotkeyTextProperty =
        AvaloniaProperty.Register<HotkeyRecorderButton, string>(
            nameof(HotkeyText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public string HotkeyText
    {
        get => GetValue(HotkeyTextProperty);
        set => SetValue(HotkeyTextProperty, value);
    }

    private bool _isRecording;
    private string _previousText = string.Empty;

    public HotkeyRecorderButton()
    {
        InitializeComponent();
        RecorderButton.Click += OnRecorderButtonClick;
        RecorderButton.LostFocus += OnRecorderButtonLostFocus;
        AddHandler(KeyDownEvent, OnKeyDownCapture, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressedCapture, RoutingStrategies.Tunnel);
        UpdateButtonContent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HotkeyTextProperty)
            UpdateButtonContent();
    }

    private void UpdateButtonContent() =>
        RecorderButton.Content = _isRecording
            ? "Press a key or mouse button…"
            : (string.IsNullOrEmpty(HotkeyText) ? "(not set)" : HotkeyText);

    private void OnRecorderButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_isRecording) return;
        _previousText = HotkeyText;
        _isRecording = true;
        UpdateButtonContent();
    }

    private void OnRecorderButtonLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_isRecording) return;
        HotkeyText = _previousText;
        ExitRecording();
    }

    private void OnKeyDownCapture(object? sender, KeyEventArgs e)
    {
        if (!_isRecording) return;

        // Ignore bare modifier presses
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin or Key.None)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            HotkeyText = _previousText;
            ExitRecording();
            e.Handled = true;
            return;
        }

        HotkeyText = BuildCombo(e.KeyModifiers, KeyName(e.Key));
        ExitRecording();
        e.Handled = true;
    }

    private void OnPointerPressedCapture(object? sender, PointerPressedEventArgs e)
    {
        if (!_isRecording) return;

        var props = e.GetCurrentPoint(this).Properties;
        string? buttonName = null;

        if (props.IsXButton2Pressed)       buttonName = "XButton2";
        else if (props.IsXButton1Pressed)  buttonName = "XButton1";
        else if (props.IsMiddleButtonPressed) buttonName = "MiddleButton";
        else if (props.IsRightButtonPressed)  buttonName = "RightButton";

        if (buttonName != null)
            HotkeyText = BuildCombo(e.KeyModifiers, buttonName);
        else
            HotkeyText = _previousText;

        ExitRecording();
        e.Handled = true;
    }

    private void ExitRecording()
    {
        _isRecording = false;
        UpdateButtonContent();
    }

    private static string BuildCombo(KeyModifiers mods, string key)
    {
        var parts = new List<string>();
        if (mods.HasFlag(KeyModifiers.Meta))    parts.Add("Meta");
        if (mods.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(KeyModifiers.Alt))     parts.Add("Alt");
        if (mods.HasFlag(KeyModifiers.Shift))   parts.Add("Shift");
        parts.Add(key);
        return string.Join("+", parts);
    }

    private static string KeyName(Key key) => key switch
    {
        Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
        Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
        _ => key.ToString(),
    };
}
