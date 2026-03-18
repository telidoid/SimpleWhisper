using System.Globalization;
using SimpleWhisper.Resources;

namespace SimpleWhisper.Services;

public class LocalizationService : ILocalizationService
{
    private readonly IAppSettingsService _settings;
    private readonly string? _appliedLanguage;

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; }
    public LanguageOption CurrentLanguage { get; private set; }
    public bool NeedsRestart => CurrentLanguage.Code != _appliedLanguage;

    public LocalizationService(IAppSettingsService settings)
    {
        _settings = settings;
        _appliedLanguage = settings.Language;

        AvailableLanguages =
        [
            new LanguageOption(null, Strings.SettingsLanguageSystem),
            new LanguageOption("en", "English"),
            new LanguageOption("ru", "Русский"),
        ];

        CurrentLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == settings.Language)
                          ?? AvailableLanguages[0];
    }

    public void SetLanguage(string? code)
    {
        _settings.Language = code;
        CurrentLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == code)
                          ?? AvailableLanguages[0];
    }

    /// <summary>
    /// Sets CurrentUICulture from the saved language setting.
    /// Must be called before any UI is created so that x:Static resource lookups use the correct culture.
    /// </summary>
    public static void ApplySavedCulture(IAppSettingsService settings)
    {
        var lang = settings.Language;
        if (string.IsNullOrEmpty(lang)) return;

        var culture = new CultureInfo(lang);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }
}
