namespace SimpleWhisper.Services;

public interface IModelCatalogService
{
    Task<IReadOnlyList<WhisperModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default);
}
