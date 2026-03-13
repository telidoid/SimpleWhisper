namespace SimpleWhisper.Services;

public class ModelDownloadService(IModelSelectionService selectionService) : IModelDownloadService
{
    private static readonly string ModelDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleWhisper", "models");

    public event Action<double>? DownloadProgressChanged;

    public async Task<string> EnsureModelExistsAsync(CancellationToken ct = default)
    {
        var model = selectionService.SelectedModel;
        if (IsModelDownloaded(model))
            return GetModelPath(model);

        var progress = new Progress<double>(p => DownloadProgressChanged?.Invoke(p));
        await DownloadModelAsync(model, progress, ct);
        return GetModelPath(model);
    }

    public bool IsModelDownloaded(WhisperModelInfo model)
    {
        var path = GetModelPath(model);
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }

    public async Task DownloadModelAsync(WhisperModelInfo model, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ModelDir);
        var modelPath = GetModelPath(model);
        var tmpPath = $"{modelPath}.tmp";

        try
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(
                model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tmpPath, FileMode.Create);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;
                if (totalBytes > 0)
                    progress?.Report((double)totalRead / totalBytes);
            }
        }
        catch
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
            throw;
        }

        File.Move(tmpPath, modelPath, overwrite: true);
    }

    private static string GetModelPath(WhisperModelInfo model) =>
        Path.Combine(ModelDir, model.FileName);
}
