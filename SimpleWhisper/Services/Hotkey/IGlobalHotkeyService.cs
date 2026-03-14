namespace SimpleWhisper.Services.Hotkey;

public interface IGlobalHotkeyService : IAsyncDisposable
{
    /// <summary>Fired on key press — start recording.</summary>
    event EventHandler? RecordingStartRequested;

    /// <summary>Fired on key release — stop recording and transcribe.</summary>
    event EventHandler? RecordingStopRequested;

    Task StartAsync(CancellationToken ct = default);

    /// <summary>Re-bind the global shortcut with a new trigger.</summary>
    Task RebindAsync(string newTrigger, CancellationToken ct = default);
}