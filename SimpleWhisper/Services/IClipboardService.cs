namespace SimpleWhisper.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
