using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using CommunityToolkit.Mvvm.ComponentModel;
using SimpleWhisper.Services;

namespace SimpleWhisper.ViewModels;

public partial class ModelsPageViewModel : ViewModelBase
{
    private readonly IModelDownloadService _downloadService;
    private readonly IModelSelectionService _selectionService;
    private readonly IModelCatalogService _catalogService;

    [ObservableProperty] private bool _isNetworkAvailable;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ObservableCollection<ModelItemViewModel> _models = [];

    public ModelsPageViewModel(IModelDownloadService downloadService, IModelSelectionService selectionService, IModelCatalogService catalogService)
    {
        _downloadService = downloadService;
        _selectionService = selectionService;
        _catalogService = catalogService;
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
        IsLoading = true;

        try
        {
            var catalog = IsNetworkAvailable
                ? await _catalogService.GetAvailableModelsAsync()
                : WhisperModelInfo.All;

            var allItems = catalog
                .Select(m => new ModelItemViewModel(m, _downloadService, _selectionService))
                .ToList();

            var visible = IsNetworkAvailable
                ? allItems
                : allItems.Where(m => m.IsDownloaded).ToList();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var item in Models)
                    item.Dispose();
                Models = new ObservableCollection<ModelItemViewModel>(visible);
            });
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsLoading = false);
        }
    }
}
