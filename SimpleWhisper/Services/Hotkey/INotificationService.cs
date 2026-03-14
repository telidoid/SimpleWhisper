namespace SimpleWhisper.Services.Hotkey;

public interface INotificationService
{
    Task NotifyAsync(string text, CancellationToken ct = default);
}