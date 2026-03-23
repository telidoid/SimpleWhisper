using System.Text.Json;
using SimpleWhisper.Core.Models;

namespace SimpleWhisper.Core.Services;

public class ModelCatalogService : IModelCatalogService
{
    private const string ApiUrl = "https://huggingface.co/api/models/ggerganov/whisper.cpp/tree/main";

    private Task<IReadOnlyList<WhisperModelInfo>>? _cachedFetch;
    private readonly object _lock = new();

    public Task<IReadOnlyList<WhisperModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return _cachedFetch ??= FetchModelsAsync(ct);
        }
    }

    public Task<IReadOnlyList<WhisperModelInfo>> FetchAvailableModelsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _cachedFetch = FetchModelsAsync(ct);
            return _cachedFetch;
        }
    }

    private static async Task<IReadOnlyList<WhisperModelInfo>> FetchModelsAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "SimpleWhisper");

            using var response = await http.GetAsync(ApiUrl, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var models = new List<WhisperModelInfo>();
            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                var filename = entry.GetProperty("path").GetString() ?? "";
                if (!filename.StartsWith("ggml-") || !filename.EndsWith(".bin"))
                    continue;

                // LFS files store actual size in lfs.size; non-LFS files use size directly
                long size = 0;
                if (entry.TryGetProperty("lfs", out var lfs) && lfs.TryGetProperty("size", out var lfsSize))
                    size = lfsSize.GetInt64();
                else if (entry.TryGetProperty("size", out var sizeProp))
                    size = sizeProp.GetInt64();

                models.Add(WhisperModelInfo.FromApiFile(filename, size));
            }

            return models.Count > 0 ? models : WhisperModelInfo.All;
        }
        catch
        {
            return WhisperModelInfo.All;
        }
    }
}
