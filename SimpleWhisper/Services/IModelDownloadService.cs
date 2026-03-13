namespace SimpleWhisper.Services;

public interface IModelDownloadService
{
    Task<string> EnsureModelExistsAsync(CancellationToken ct = default);
    event Action<double>? DownloadProgressChanged;
}
