namespace SimpleWhisper.Services;

public class ModelSelectionService : IModelSelectionService
{
    private static readonly string SelectionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleWhisper", "selected-model.txt");

    public event Action<WhisperModelInfo>? SelectedModelChanged;

    private WhisperModelInfo _selectedModel;

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

    public ModelSelectionService(IAppSettingsService appSettings)
    {
        _selectedModel = Load(appSettings.ModelsDirectory);
    }

    private static WhisperModelInfo Load(string modelsDirectory)
    {
        if (!File.Exists(SelectionPath))
            return WhisperModelInfo.Default;

        var name = File.ReadAllText(SelectionPath).Trim();

        var filePath = Path.Combine(modelsDirectory, $"ggml-{name}.bin");
        if (File.Exists(filePath))
        {
            var fi = new FileInfo(filePath);
            if (fi.Length > 0)
                return WhisperModelInfo.FromApiFile(fi.Name, fi.Length);
        }

        return WhisperModelInfo.Default;
    }

    private static void Save(WhisperModelInfo model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SelectionPath)!);
        File.WriteAllText(SelectionPath, model.Name);
    }
}
