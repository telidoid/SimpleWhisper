namespace SimpleWhisper.Services.Hotkey;

public interface IGlobalHotkeyService : IAsyncDisposable
{
    /// <summary>Fired on key press — start recording.</summary>
    event EventHandler? RecordingStartRequested;

    /// <summary>Fired on key release — stop recording and transcribe.</summary>
    event EventHandler? RecordingStopRequested;

    Task StartAsync(CancellationToken ct = default);
}