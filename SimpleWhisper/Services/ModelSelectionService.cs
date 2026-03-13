namespace SimpleWhisper.Services;

public class ModelSelectionService : IModelSelectionService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleWhisper", "selected-model.txt");

    public event Action<WhisperModelInfo>? SelectedModelChanged;

    public WhisperModelInfo SelectedModel
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Save(value);
            SelectedModelChanged?.Invoke(value);
        }
    } = Load();

    private static WhisperModelInfo Load()
    {
        if (!File.Exists(SettingsPath)) 
            return WhisperModelInfo.Default;
        
        var name = File.ReadAllText(SettingsPath).Trim();
        var model = WhisperModelInfo.All.FirstOrDefault(m => m.Name == name);
        return model ?? WhisperModelInfo.Default;
    }

    private static void Save(WhisperModelInfo model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, model.Name);
    }
}
