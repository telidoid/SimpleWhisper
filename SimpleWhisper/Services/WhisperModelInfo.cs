namespace SimpleWhisper.Services;

public enum ModelLanguage { Multilingual, EnglishOnly }

public record WhisperModelInfo(
    string Name,
    string FileName,
    string DisplaySize,
    long SizeBytes,
    ModelLanguage Language,
    bool IsDownloaded = false)
{
    public string DownloadUrl => $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{FileName}";

    public bool CanTranslateToEnglish => Language == ModelLanguage.Multilingual;

    public static readonly IReadOnlyList<WhisperModelInfo> All =
    [
        new("tiny",     "ggml-tiny.bin",     "~75 MB",   75_000_000L,    ModelLanguage.Multilingual),
        new("base",     "ggml-base.bin",     "~142 MB",  142_000_000L,   ModelLanguage.Multilingual),
        new("small",    "ggml-small.bin",    "~466 MB",  466_000_000L,   ModelLanguage.Multilingual),
        new("medium",   "ggml-medium.bin",   "~1.5 GB",  1_500_000_000L, ModelLanguage.Multilingual),
        new("large-v3", "ggml-large-v3.bin", "~3.1 GB",  3_100_000_000L, ModelLanguage.Multilingual),
    ];

    public static readonly WhisperModelInfo Default = All[0];

    public static WhisperModelInfo FromApiFile(string rfilename, long sizeBytes)
    {
        var name = rfilename["ggml-".Length..^".bin".Length];
        var language = name.Contains(".en") ? ModelLanguage.EnglishOnly : ModelLanguage.Multilingual;
        return new WhisperModelInfo(name, rfilename, FormatBytes(sizeBytes), sizeBytes, language);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_000_000_000)
            return $"~{bytes / 1_000_000_000.0:F1} GB";
        
        return $"~{bytes / 1_000_000} MB";
    }
}
