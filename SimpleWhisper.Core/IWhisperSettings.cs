namespace SimpleWhisper.Core;

public interface IWhisperSettings
{
    bool UseHardwareAcceleration { get; }
    string ModelsDirectory { get; }
}
