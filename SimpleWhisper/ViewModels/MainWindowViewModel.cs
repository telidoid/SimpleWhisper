using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimpleWhisper.ViewModels;

public partial class MainWindowViewModel(MainPageViewModel mainPage, ModelsPageViewModel modelsPage, SettingsPageViewModel settingsPage) : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentPage = mainPage;

    public string Title { get; } = $"SimpleWhisper v{(typeof(MainWindowViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?").Split('+')[0]}";

    public MainPageViewModel MainPage { get; } = mainPage;
    public ModelsPageViewModel ModelsPage { get; } = modelsPage;
    public SettingsPageViewModel SettingsPage { get; } = settingsPage;

    [RelayCommand]
    private void NavigateTo(ViewModelBase page)
    {
        CurrentPage = page;
    }
}