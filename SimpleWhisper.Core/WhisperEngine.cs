using SimpleWhisper.Core.Models;
using SimpleWhisper.Core.Services;

namespace SimpleWhisper.Core;

public sealed class WhisperEngine : IAsyncDisposable
{
    private readonly ModelSelectionService _selectionService;
    private readonly ModelDownloadService _downloadService;
    private readonly ModelCatalogService _catalogService;
    private readonly WhisperTranscriptionService _transcriptionService;

    public WhisperEngine(WhisperEngineOptions? options = null)
    {
        options ??= new WhisperEngineOptions();
        var settings = new InlineWhisperSettings(options);
        _selectionService = new ModelSelectionService(settings);
        _downloadService = new ModelDownloadService(_selectionService, settings);
        _catalogService = new ModelCatalogService();
        _transcriptionService = new WhisperTranscriptionService(
            _downloadService, _selectionService, settings);
    }

    public WhisperModelInfo SelectedModel
    {
        get => _selectionService.SelectedModel;
        set => _selectionService.SelectedModel = value;
    }

    public event Action<double>? DownloadProgressChanged
    {
        add => _downloadService.DownloadProgressChanged += value;
        remove => _downloadService.DownloadProgressChanged -= value;
    }

    public Task<string> TranscribeAsync(string wavFilePath, CancellationToken ct = default)
        => _transcriptionService.TranscribeAsync(wavFilePath, ct);

    public Task<string> EnsureModelExistsAsync(CancellationToken ct = default)
        => _downloadService.EnsureModelExistsAsync(ct);

    public bool IsModelDownloaded(WhisperModelInfo model)
        => _downloadService.IsModelDownloaded(model);

    public IReadOnlyList<WhisperModelInfo> GetDownloadedModels()
        => _downloadService.GetDownloadedModels();

    public Task<IReadOnlyList<WhisperModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default)
        => _catalogService.GetAvailableModelsAsync(ct);

    public Task DownloadModelAsync(WhisperModelInfo model, IProgress<double>? progress = null, CancellationToken ct = default)
        => _downloadService.DownloadModelAsync(model, progress, ct);

    public void DeleteModel(WhisperModelInfo model)
        => _downloadService.DeleteModel(model);

    public static GpuInfo DetectGpu() => GpuDetectionService.Detect();

    public async ValueTask DisposeAsync()
        => await _transcriptionService.DisposeAsync();

    private sealed class InlineWhisperSettings(WhisperEngineOptions options) : IWhisperSettings
    {
        public bool UseHardwareAcceleration => options.UseHardwareAcceleration;
        public string ModelsDirectory => options.ModelsDirectory;
    }
}
