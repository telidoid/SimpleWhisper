namespace SimpleWhisper.Services;

public interface ITextPasteService
{
    bool IsAvailable { get; }
    Task PasteAsync(string text, CancellationToken ct = default);
}
