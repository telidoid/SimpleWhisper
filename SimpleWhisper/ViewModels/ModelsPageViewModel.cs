using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using CommunityToolkit.Mvvm.ComponentModel;
using SimpleWhisper.Services;

namespace SimpleWhisper.ViewModels;

public partial class ModelsPageViewModel : ViewModelBase
{
    private readonly IModelDownloadService _downloadService;
    private readonly IModelSelectionService _selectionService;

    [ObservableProperty] private bool _isNetworkAvailable;
    [ObservableProperty] private ObservableCollection<ModelItemViewModel> _models = new();

    public ModelsPageViewModel(IModelDownloadService downloadService, IModelSelectionService selectionService)
    {
        _downloadService = downloadService;
        _selectionService = selectionService;
        Refresh();
    }

    private void Refresh()
    {
        IsNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();

        var allItems = WhisperModelInfo.All
            .Select(m => new ModelItemViewModel(m, _downloadService, _selectionService))
            .ToList();

        Models = IsNetworkAvailable
            ? new ObservableCollection<ModelItemViewModel>(allItems)
            : new ObservableCollection<ModelItemViewModel>(allItems.Where(m => m.IsDownloaded));
    }
}
