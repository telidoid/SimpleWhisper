using Whisper.net;

namespace SimpleWhisper.Services;

public class WhisperTranscriptionService(IModelDownloadService modelService) : IWhisperTranscriptionService
{
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private string? _loadedModelPath;

    public async Task<string> TranscribeAsync(string wavFilePath, CancellationToken ct = default)
    {
        var modelPath = await modelService.EnsureModelExistsAsync(ct);
        if (_loadedModelPath != modelPath)
        {
            if (_processor is not null)
            {
                await _processor.DisposeAsync();
                _processor = null;
            }
            _factory?.Dispose();

            _loadedModelPath = modelPath;
            _factory = WhisperFactory.FromPath(modelPath);
            _processor = _factory
                .CreateBuilder()
                .WithLanguage("en")
                .Build();
        }

        await using var fileStream = File.OpenRead(wavFilePath);
        var segments = new List<string>();

        await foreach (var segment in _processor!.ProcessAsync(fileStream, ct))
        {
            segments.Add(segment.Text);
        }

        return string.Join(" ", segments).Trim();
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null)
        {
            await _processor.DisposeAsync();
            _processor = null;
        }
        _factory?.Dispose();
        _factory = null;
    }
}