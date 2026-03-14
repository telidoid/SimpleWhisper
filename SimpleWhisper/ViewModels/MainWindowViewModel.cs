using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimpleWhisper.ViewModels;

public partial class MainWindowViewModel(MainPageViewModel mainPage, ModelsPageViewModel modelsPage, SettingsPageViewModel settingsPage) : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentPage = mainPage;

    public MainPageViewModel MainPage { get; } = mainPage;
    public ModelsPageViewModel ModelsPage { get; } = modelsPage;
    public SettingsPageViewModel SettingsPage { get; } = settingsPage;

    [RelayCommand]
    private void NavigateTo(ViewModelBase page)
    {
        CurrentPage = page;
    }
}