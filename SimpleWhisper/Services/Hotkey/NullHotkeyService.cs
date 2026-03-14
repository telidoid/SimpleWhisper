namespace SimpleWhisper.Services.Hotkey;

public sealed class NullHotkeyService : IGlobalHotkeyService
{
#pragma warning disable CS0067 // Events required by interface, intentionally never raised
    public event EventHandler? RecordingStartRequested;
    public event EventHandler? RecordingStopRequested;
#pragma warning restore CS0067

    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}