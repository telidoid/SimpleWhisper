namespace SimpleWhisper.Services;

public class ModelDownloadService : IModelDownloadService
{
    private static readonly string ModelDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleWhisper", "models");

    private static readonly string ModelPath = Path.Combine(ModelDir, "ggml-tiny.bin");

    private const string DownloadUrl =
        "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin";

    public event Action<double>? DownloadProgressChanged;

    public async Task<string> EnsureModelExistsAsync(CancellationToken ct = default)
    {
        if (File.Exists(ModelPath) && new FileInfo(ModelPath).Length > 0)
            return ModelPath;

        Directory.CreateDirectory(ModelDir);
        var tmpPath = ModelPath + ".tmp";

        try
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, ct);
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
                    DownloadProgressChanged?.Invoke((double)totalRead / totalBytes);
            }
        }
        catch
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
            throw;
        }

        File.Move(tmpPath, ModelPath);
        return ModelPath;
    }
}
