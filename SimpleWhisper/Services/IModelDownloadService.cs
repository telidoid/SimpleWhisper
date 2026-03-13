namespace SimpleWhisper.Services;

public interface IModelDownloadService
{
    event Action<double>? DownloadProgressChanged;
    Task<string> EnsureModelExistsAsync(CancellationToken ct = default);
    bool IsModelDownloaded(WhisperModelInfo model);
    Task DownloadModelAsync(WhisperModelInfo model, IProgress<double>? progress = null, CancellationToken ct = default);
}
