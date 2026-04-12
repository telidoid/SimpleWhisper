using SimpleWhisper.Core.Models;

namespace SimpleWhisper.Core.Services;

public interface IModelSelectionService
{
    WhisperModelInfo SelectedModel { get; set; }
    event Action<WhisperModelInfo>? SelectedModelChanged;
}
