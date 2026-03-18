namespace SimpleWhisper.Services;

public interface ITextPasteService
{
    bool IsAvailable { get; }

    /// <summary>
    /// Captures the currently focused window for later paste targeting.
    /// Call before the app might steal focus (e.g. when recording starts).
    /// </summary>
    void CaptureTargetWindow() { }

    Task PasteAsync(string text, CancellationToken ct = default);
}
