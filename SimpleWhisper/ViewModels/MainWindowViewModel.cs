using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimpleWhisper.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentPage;

    public MainPageViewModel MainPage { get; }
    public ModelsPageViewModel ModelsPage { get; }

    public MainWindowViewModel(MainPageViewModel mainPage, ModelsPageViewModel modelsPage)
    {
        MainPage = mainPage;
        ModelsPage = modelsPage;
        _currentPage = mainPage;
    }

    [RelayCommand]
    private void NavigateTo(ViewModelBase page)
    {
        CurrentPage = page;
    }
}
