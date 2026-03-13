using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleWhisper.Services;

namespace SimpleWhisper.ViewModels;

public partial class ModelItemViewModel : ViewModelBase
{
    private readonly IModelDownloadService _downloadService;
    private readonly IModelSelectionService _selectionService;

    public WhisperModelInfo Model { get; }
    public string Name => Model.Name;
    public string DisplaySize => Model.DisplaySize;
    public long SizeBytes => Model.SizeBytes;
    public ModelLanguage Language => Model.Language;
    public bool CanTranslateToEnglish => Model.CanTranslateToEnglish;

    [ObservableProperty] private bool _isDownloaded;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isSelected;

    public ModelItemViewModel(WhisperModelInfo model, IModelDownloadService downloadService, IModelSelectionService selectionService)
    {
        Model = model;
        _downloadService = downloadService;
        _selectionService = selectionService;
        _isDownloaded = downloadService.IsModelDownloaded(model);
        _isSelected = selectionService.SelectedModel == model;
        selectionService.SelectedModelChanged += OnSelectedModelChanged;
    }

    private void OnSelectedModelChanged(WhisperModelInfo selected)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsSelected = selected == Model;
        });
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        IsDownloading = true;
        DownloadProgress = 0;
        ErrorMessage = null;

        try
        {
            var progress = new Progress<double>(p =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = p));

            await _downloadService.DownloadModelAsync(Model, progress);
            IsDownloaded = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private bool CanDownload() => !IsDownloaded && !IsDownloading;

    [RelayCommand(CanExecute = nameof(CanSelect))]
    private void Select() => _selectionService.SelectedModel = Model;

    private bool CanSelect() => IsDownloaded && !IsSelected;

    partial void OnIsDownloadedChanged(bool value)
    {
        DownloadCommand.NotifyCanExecuteChanged();
        SelectCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsDownloadingChanged(bool value) => DownloadCommand.NotifyCanExecuteChanged();

    partial void OnIsSelectedChanged(bool value) => SelectCommand.NotifyCanExecuteChanged();
}
