namespace SimpleWhisper.Services;

public class ModelSelectionService : IModelSelectionService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleWhisper", "selected-model.txt");

    private WhisperModelInfo _selectedModel;

    public event Action<WhisperModelInfo>? SelectedModelChanged;

    public ModelSelectionService()
    {
        _selectedModel = Load();
    }

    public WhisperModelInfo SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (_selectedModel == value) return;
            _selectedModel = value;
            Save(value);
            SelectedModelChanged?.Invoke(value);
        }
    }

    private static WhisperModelInfo Load()
    {
        if (File.Exists(SettingsPath))
        {
            var name = File.ReadAllText(SettingsPath).Trim();
            var model = WhisperModelInfo.All.FirstOrDefault(m => m.Name == name);
            if (model is not null) return model;
        }
        return WhisperModelInfo.Default;
    }

    private static void Save(WhisperModelInfo model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, model.Name);
    }
}
