namespace SimpleWhisper.Core;

public sealed class WhisperEngineOptions
{
    public bool UseHardwareAcceleration { get; set; } = true;

    public string ModelsDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleWhisper", "models");
}
