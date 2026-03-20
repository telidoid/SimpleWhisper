using System.Globalization;

namespace SimpleWhisper.Resources;

/// <summary>
/// All user-facing strings, resolved once from <see cref="CultureInfo.CurrentUICulture"/>
/// at first access. Fully NativeAOT-safe — no ResourceManager, no satellite assemblies.
/// </summary>
public static class Strings
{
    private static readonly string Lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    private static string L(string en, string ru) => Lang == "ru" ? ru : en;

    // Tray icon
    public static string TrayShow => L("Show", "Показать");
    public static string TrayQuit => L("Quit", "Выйти");
    public static string TrayTooltip => "SimpleWhisper";
    public static string TrayTooltipRecording => L("SimpleWhisper - Recording...", "SimpleWhisper — запись...");
    public static string TrayTooltipTranscribing => L("SimpleWhisper - Transcribing...", "SimpleWhisper — транскрипция...");

    // Navigation
    public static string NavHeader => "SimpleWhisper";
    public static string NavMain => L("Main", "Главная");
    public static string NavModels => L("Models", "Модели");
    public static string NavSettings => L("Settings", "Настройки");

    // Main page
    public static string TranscribedTextWatermark => L("Transcribed text will appear here...", "Транскрибированный текст появится здесь...");
    public static string RecordButtonStart => L("Start Recording", "Начать запись");
    public static string RecordButtonStop => L("Stop Recording", "Остановить запись");
    public static string RecordButtonCancel => L("Cancel", "Отмена");
    public static string ClearTextButton => L("Clear", "Очистить");

    // Status messages
    public static string StatusReady => L("Ready", "Готово");
    public static string StatusCheckingModel => L("Checking model...", "Проверка модели...");
    public static string StatusRecording => L("Recording... Click to stop.", "Запись... Нажмите для остановки.");
    public static string StatusStopping => L("Stopping recording...", "Остановка записи...");
    public static string StatusTranscribing => L("Transcribing...", "Транскрипция...");
    public static string StatusCancelled => L("Cancelled", "Отменено");
    public static string StatusError => L("Error: {0}", "Ошибка: {0}");

    // Settings — General
    public static string SettingsGeneral => L("General", "Основные");
    public static string SettingsMinimizeToTray => L("Minimize to system tray when closing", "Сворачивать в системный трей при закрытии");
    public static string SettingsMinimizeToTrayHelp => L(
        "The app will continue running in the background and can be restored from the tray icon",
        "Приложение продолжит работу в фоне и его можно будет восстановить из значка в трее");
    public static string SettingsAutoStart => L("Launch at system startup", "Запускать при старте системы");
    public static string SettingsAutoStartHelp => L(
        "Automatically start SimpleWhisper when you log in",
        "Автоматически запускать SimpleWhisper при входе в систему");

    // Settings — Theme
    public static string SettingsTheme => L("Theme", "Тема");
    public static string SettingsThemeSystem => L("System default", "Системная");
    public static string SettingsThemeLight => L("Light", "Светлая");
    public static string SettingsThemeDark => L("Dark", "Тёмная");

    // Settings — Language
    public static string SettingsLanguage => L("Language", "Язык");
    public static string SettingsLanguageSystem => L("System default", "Системный язык");
    public static string SettingsLanguageHelp => L("Restart required to apply language changes.", "Для применения изменений языка требуется перезапуск.");

    // Settings — Microphone
    public static string SettingsMicrophone => L("Microphone", "Микрофон");
    public static string SettingsRefreshDeviceList => L("Refresh device list", "Обновить список устройств");
    public static string SettingsMicrophoneHelp => L("Select which microphone to use for recording", "Выберите микрофон для записи");

    // Settings — Recording Mode
    public static string SettingsRecordingMode => L("Recording Mode", "Режим записи");
    public static string SettingsRecordingModeHold => L("Hold (push-to-talk)", "Удержание (push-to-talk)");
    public static string SettingsRecordingModeToggle => L(
        "Toggle (press once to start, press again to stop)",
        "Переключение (нажать для начала, нажать снова для остановки)");

    // Settings — Performance
    public static string SettingsPerformance => L("Performance", "Производительность");
    public static string SettingsRestartRequired => L("Restart required to apply", "Требуется перезапуск");
    public static string SettingsRestartNow => L("Restart Now", "Перезапустить");
    public static string GpuAccelCuda => L("Use GPU acceleration ({0} via CUDA)", "Использовать ускорение GPU ({0} через CUDA)");
    public static string GpuAccelVulkan => L("Use GPU acceleration ({0} via Vulkan)", "Использовать ускорение GPU ({0} через Vulkan)");
    public static string GpuAccelUnavailable => L("GPU acceleration unavailable", "Ускорение GPU недоступно");

    // Settings — Clipboard
    public static string SettingsClipboard => L("Clipboard", "Буфер обмена");
    public static string SettingsCopyToClipboard => L("Copy transcription to clipboard after each recording", "Копировать транскрипцию в буфер обмена после каждой записи");
    public static string SettingsShowNotification => L("Show desktop notification after each recording", "Показывать уведомление на рабочем столе после каждой записи");
    public static string SettingsPasteIntoWindow => L("Paste transcription into focused window", "Вставлять транскрипцию в активное окно");
    public static string SettingsPasteWaylandTooltip => L("Not available on Wayland.", "Недоступно на Wayland.");

    // Settings — Global Hotkey
    public static string SettingsGlobalHotkey => L("Global Hotkey", "Глобальное сочетание клавиш");
    public static string SettingsWaylandHotkeyHelp => L(
        "On Wayland, the shortcut is managed by your compositor. Use the button below to configure it in your system's keyboard shortcuts settings.",
        "На Wayland сочетание клавиш управляется вашим композитором. Используйте кнопку ниже для настройки в системных параметрах клавиатуры.");
    public static string SettingsOpenSystemShortcuts => L("Open System Shortcuts Settings", "Открыть системные сочетания клавиш");

    // Settings — Models Directory
    public static string SettingsModelsDirectory => L("Models Directory", "Папка моделей");
    public static string SettingsBrowse => L("Browse…", "Обзор…");
    public static string SettingsOpenInFileManager => L("Open in file manager", "Открыть в файловом менеджере");
    public static string SettingsSelectModelsDirectory => L("Select Models Directory", "Выберите папку моделей");

    // Settings — System Information
    public static string SettingsSystemInfo => L("System Information", "Сведения о системе");
    public static string SettingsOS => L("Operating System", "Операционная система");
    public static string SettingsDE => L("Desktop Environment", "Рабочий стол");
    public static string SettingsDisplayServer => L("Display Server", "Сервер отображения");

    // Models page
    public static string ModelsInUseLabel => L("In use:", "Используется:");
    public static string ModelsNoInternet => L("No internet connection — showing downloaded models only", "Нет подключения к интернету — показаны только загруженные модели");
    public static string ModelsFetching => L("Fetching model list…", "Загрузка списка моделей…");
    public static string ModelsSearchWatermark => L("Search models…", "Поиск моделей…");
    public static string ModelsShowDownloadedOnly => L("Show downloaded only", "Только загруженные");
    public static string ModelsRefreshList => L("Refresh model list", "Обновить список моделей");
    public static string ModelsInUseBadge => L("In use", "Используется");
    public static string ModelsUseButton => L("Use", "Выбрать");
    public static string ModelsCancelDownload => L("Cancel download", "Отменить загрузку");
    public static string ModelsUseThisModel => L("Use this model", "Использовать эту модель");
    public static string ModelsRemoveModel => L("Remove model from disk", "Удалить модель с диска");
    public static string ModelsDownloadModel => L("Download model", "Загрузить модель");

    // Hotkey recorder
    public static string HotkeyRecording => L("Press a key or mouse button…", "Нажмите клавишу или кнопку мыши…");
    public static string HotkeyNotSet => L("(not set)", "(не задано)");
}
