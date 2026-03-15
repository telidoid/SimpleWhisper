using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    [ObservableProperty] private string _selectedModelName = string.Empty;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _showDownloadedOnly;

    private List<ModelItemViewModel> _allModels = [];

    public ModelsPageViewModel(IModelDownloadService downloadService, IModelSelectionService selectionService, IModelCatalogService catalogService)
    {
        _downloadService = downloadService;
        _selectionService = selectionService;
        _catalogService = catalogService;
        _selectedModelName = selectionService.SelectedModel.Name;
        selectionService.SelectedModelChanged += m =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedModelName = m.Name);
        _ = RefreshAsync();
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnShowDownloadedOnlyChanged(bool value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allModels.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            filtered = filtered.Where(m => FuzzyMatch(m.Name, SearchQuery));
        if (ShowDownloadedOnly)
            filtered = filtered.Where(m => m.IsDownloaded);
        Models = new ObservableCollection<ModelItemViewModel>(filtered);
    }

    private static bool FuzzyMatch(string source, string query)
    {
        var s = source.AsSpan();
        var q = query.AsSpan();
        int si = 0;
        for (int qi = 0; qi < q.Length; qi++)
        {
            var c = char.ToLowerInvariant(q[qi]);
            bool found = false;
            while (si < s.Length)
            {
                if (char.ToLowerInvariant(s[si++]) == c) { found = true; break; }
            }
            if (!found) return false;
        }
        return true;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
        IsLoading = true;

        try
        {
            var catalog = IsNetworkAvailable
                ? await _catalogService.GetAvailableModelsAsync()
                : _downloadService.GetDownloadedModels();

            var visible = catalog
                .Select(m => new ModelItemViewModel(m, _downloadService, _selectionService))
                .ToList();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Preserve ViewModels that are currently downloading
                var merged = visible.Select(newItem =>
                {
                    var downloading = _allModels.FirstOrDefault(old => old.Name == newItem.Name && old.IsDownloading);
                    if (downloading != null)
                    {
                        newItem.Dispose();
                        return downloading;
                    }
                    return newItem;
                }).ToList();

                foreach (var item in _allModels.Where(old => !merged.Contains(old)))
                    item.Dispose();

                _allModels = merged;
                ApplyFilter();
            });
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsLoading = false);
        }
    }
}
