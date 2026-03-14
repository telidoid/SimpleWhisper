namespace SimpleWhisper.Services.Hotkey;

public sealed class NullNotificationService : INotificationService
{
    public Task NotifyAsync(string text, CancellationToken ct = default) => Task.CompletedTask;
}