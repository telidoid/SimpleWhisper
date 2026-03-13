# SimpleWhisper – Avalonia Best Practices

Accumulated knowledge about Avalonia 11 patterns used in this project.

---

## ViewModel-Based Navigation

Use a `CurrentPage` observable property of type `ViewModelBase` on the shell ViewModel. Bind a `ContentControl` to it. Map ViewModels to Views using `DataTemplate` in `Application.DataTemplates` (not `Window.DataTemplates` — must be global scope to work with `ContentControl`).

```xml
<!-- App.axaml -->
<Application.DataTemplates>
    <DataTemplate DataType="vm:MainPageViewModel">
        <views:MainPageView />
    </DataTemplate>
</Application.DataTemplates>
```

The `ContentControl` resolves the correct View automatically when `CurrentPage` changes.

---

## Compiled Bindings

Always declare `x:DataType` on every Window and UserControl. This enables compile-time binding validation and better performance.

```xml
<UserControl x:DataType="vm:MainPageViewModel">
```

Without `x:DataType`, compiled bindings (enabled by default in this project via `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`) will fail at runtime.

---

## Page UserControls

Pages are `UserControl` subclasses, not Windows. Each has:
- An `.axaml` file with `x:DataType` pointing to its ViewModel
- A `.axaml.cs` code-behind that calls `InitializeComponent()` in the constructor

Keep all page views under `Views/` and all page ViewModels under `ViewModels/`.

---

## CommunityToolkit.Mvvm Source Generators

All ViewModel classes must be `partial`. Use attributes instead of boilerplate:

- `[ObservableProperty]` on a private `_camelCase` field → generates public `PascalCase` property with `INotifyPropertyChanged`
- `[RelayCommand]` on a method `DoSomethingAsync()` → generates `DoSomethingCommand`
- Use `partial void OnXxxChanged(T value)` to react to property changes

```csharp
public partial class MyViewModel : ViewModelBase
{
    [ObservableProperty] private string _text = string.Empty;

    partial void OnTextChanged(string value) { ... }

    [RelayCommand]
    private async Task DoSomethingAsync() { ... }
}
```

---

## Dependency Injection (Microsoft.Extensions.DI)

Register page ViewModels as **singletons** so state (e.g. transcribed text) is preserved when navigating away and back. Only use `AddTransient` for stateless services like `AudioRecordingService`.

Services are accessed via `Program.AppHost.Services` — the host is bootstrapped before the Avalonia app starts.

---

## UI Thread Updates from Async Code

Always marshal back to the UI thread when updating observable properties from background/async contexts:

```csharp
Avalonia.Threading.Dispatcher.UIThread.Post(() => MyProperty = value);
```

---

## Layout Patterns

- Use `Grid` with `ColumnDefinitions` / `RowDefinitions` for fixed splits (e.g. sidebar + content)
- Use `DockPanel` for simple top/bottom/fill layouts within a page
- Use `DynamicResource` for theme-aware colors (e.g. `{DynamicResource SystemControlForegroundBaseMediumLowBrush}`) so they adapt to light/dark mode

---

## Styles and Style Classes

### Where styles live

- **Component-specific styles** → define in `<Window.Styles>` or `<UserControl.Styles>` inside the component's own `.axaml` file
- **Shared styles** → define in `Styles/AppStyles.axaml`, included globally via `App.axaml`

```xml
<!-- App.axaml -->
<Application.Styles>
    <FluentTheme />
    <StyleInclude Source="/Styles/AppStyles.axaml" />
</Application.Styles>
```

### Defining and using style classes

```xml
<!-- MainWindow.axaml — component-local styles -->
<Window.Styles>
    <Style Selector="Border.sidebar">
        <Setter Property="Padding" Value="8" />
        <Setter Property="BorderThickness" Value="0,0,1,0" />
        <Setter Property="BorderBrush" Value="{DynamicResource SystemControlForegroundBaseMediumLowBrush}" />
    </Style>
    <Style Selector="Button.nav-item">
        <Setter Property="HorizontalAlignment" Value="Stretch" />
        <Setter Property="HorizontalContentAlignment" Value="Left" />
        <Setter Property="Padding" Value="12,8" />
    </Style>
</Window.Styles>
```

```xml
<!-- Apply with Classes attribute -->
<Border Classes="sidebar">
<Button Classes="nav-item" Content="Main" ... />
```

**Pitfall:** `<Thickness x:Key="...">` and other typed resources cannot be placed directly as children of `<Styles>`. They require a `<Styles.Resources><ResourceDictionary>` wrapper — or just inline the values in `<Setter>` if only used in one place.

---

## CommandParameter Binding

Pass ViewModel references as `CommandParameter` to navigate:

```xml
<Button Command="{Binding NavigateToCommand}"
        CommandParameter="{Binding MainPage}" />
```

The command method signature must accept `ViewModelBase` (or the specific type):

```csharp
[RelayCommand]
private void NavigateTo(ViewModelBase page) => CurrentPage = page;
```
