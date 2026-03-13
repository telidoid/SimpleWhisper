namespace SimpleWhisper.Services;

public interface IModelSelectionService
{
    WhisperModelInfo SelectedModel { get; set; }
    event Action<WhisperModelInfo>? SelectedModelChanged;
}
