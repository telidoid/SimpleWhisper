namespace SimpleWhisper.Services;

public sealed class NullTextPasteService : ITextPasteService
{
    public bool IsAvailable => false;
    public Task PasteAsync(string text, CancellationToken ct = default) => Task.CompletedTask;
}
