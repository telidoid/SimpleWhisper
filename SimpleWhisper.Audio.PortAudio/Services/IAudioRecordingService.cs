namespace SimpleWhisper.Audio.PortAudio.Services;

public interface IAudioRecordingService
{
    bool IsRecording { get; }
    Task<string> StartRecordingAsync(CancellationToken ct = default);
    Task<string> StopRecordingAsync();
}
