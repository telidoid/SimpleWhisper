namespace SimpleWhisper.Services;

public interface IModelCatalogService
{
    /// <summary>
    /// Returns the cached list of available models, or fetches from the remote API if no cache exists.
    /// </summary>
    Task<IReadOnlyList<WhisperModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Always fetches a fresh list of available models from the remote API, replacing the cache.
    /// </summary>
    Task<IReadOnlyList<WhisperModelInfo>> FetchAvailableModelsAsync(CancellationToken ct = default);
}
