using Whisper.net;

namespace SimpleWhisper.Services;

public class WhisperTranscriptionService : IWhisperTranscriptionService
{
    private readonly IModelDownloadService _modelService;
    private WhisperProcessor? _processor;

    public WhisperTranscriptionService(IModelDownloadService modelService)
    {
        _modelService = modelService;
    }

    public async Task<string> TranscribeAsync(string wavFilePath, CancellationToken ct = default)
    {
        if (_processor is null)
        {
            var modelPath = await _modelService.EnsureModelExistsAsync(ct);
            _processor = WhisperFactory
                .FromPath(modelPath)
                .CreateBuilder()
                .WithLanguage("en")
                .Build();
        }

        await using var fileStream = File.OpenRead(wavFilePath);
        var segments = new List<string>();

        await foreach (var segment in _processor.ProcessAsync(fileStream, ct))
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
    }
}
