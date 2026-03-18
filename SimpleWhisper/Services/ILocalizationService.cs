namespace SimpleWhisper.Services;

public record LanguageOption(string? Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public interface ILocalizationService
{
    IReadOnlyList<LanguageOption> AvailableLanguages { get; }
    LanguageOption CurrentLanguage { get; }
    void SetLanguage(string? code);
    bool NeedsRestart { get; }
}
