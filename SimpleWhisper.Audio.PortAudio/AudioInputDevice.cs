namespace SimpleWhisper.Audio.PortAudio;

public record AudioInputDevice(int Index, string Name, string? DisplayName = null)
{
    public static AudioInputDevice SystemDefault { get; } = new(-1, "System Default");

    public override string ToString() => DisplayName ?? Name;
}
