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
    public string DownloadUrl =>
        $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{FileName}";

    public bool CanTranslateToEnglish => Language == ModelLanguage.Multilingual;

    public static readonly IReadOnlyList<WhisperModelInfo> All =
    [
        new WhisperModelInfo("tiny",     "ggml-tiny.bin",     "~75 MB",   75_000_000L,    ModelLanguage.Multilingual),
        new WhisperModelInfo("base",     "ggml-base.bin",     "~142 MB",  142_000_000L,   ModelLanguage.Multilingual),
        new WhisperModelInfo("small",    "ggml-small.bin",    "~466 MB",  466_000_000L,   ModelLanguage.Multilingual),
        new WhisperModelInfo("medium",   "ggml-medium.bin",   "~1.5 GB",  1_500_000_000L, ModelLanguage.Multilingual),
        new WhisperModelInfo("large-v3", "ggml-large-v3.bin", "~3.1 GB",  3_100_000_000L, ModelLanguage.Multilingual),
    ];

    public static readonly WhisperModelInfo Default = All[0];
}
