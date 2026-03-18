namespace SimpleWhisper.Services;

public interface IAutoStartService
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}
