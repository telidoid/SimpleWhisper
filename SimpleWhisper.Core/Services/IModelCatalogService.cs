using SimpleWhisper.Core.Models;

namespace SimpleWhisper.Core.Services;

public interface IModelCatalogService
{
    Task<IReadOnlyList<WhisperModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WhisperModelInfo>> FetchAvailableModelsAsync(CancellationToken ct = default);
}
