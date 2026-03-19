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

    public bool IsOnMainPage => CurrentPage == MainPage;
    public bool IsOnModelsPage => CurrentPage == ModelsPage;
    public bool IsOnSettingsPage => CurrentPage == SettingsPage;

    partial void OnCurrentPageChanged(ViewModelBase value)
    {
        OnPropertyChanged(nameof(IsOnMainPage));
        OnPropertyChanged(nameof(IsOnModelsPage));
        OnPropertyChanged(nameof(IsOnSettingsPage));
    }

    [RelayCommand]
    private void NavigateTo(ViewModelBase page)
    {
        CurrentPage = page;
    }
}