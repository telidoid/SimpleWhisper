namespace SimpleWhisper.Services;

public interface IWhisperTranscriptionService : IAsyncDisposable
{
    Task<string> TranscribeAsync(string wavFilePath, CancellationToken ct = default);
}
