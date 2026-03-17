using Whisper.net;
using Whisper.net.LibraryLoader;

namespace SimpleWhisper.Services;

public class WhisperTranscriptionService : IWhisperTranscriptionService
{
    private readonly IModelDownloadService _modelService;
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private string? _loadedModelPath;
    public WhisperTranscriptionService(IModelDownloadService modelService, IModelSelectionService modelSelectionService, IAppSettingsService appSettings)
    {
        _modelService = modelService;
        RuntimeOptions.RuntimeLibraryOrder = appSettings.UseHardwareAcceleration
            ? [RuntimeLibrary.Cuda, RuntimeLibrary.CoreML, RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu, RuntimeLibrary.CpuNoAvx]
            : [RuntimeLibrary.Cpu, RuntimeLibrary.CpuNoAvx];
        modelSelectionService.SelectedModelChanged += _ => UnloadModel();
    }

    private void UnloadModel()
    {
        _processor?.Dispose();
        _processor = null;
        _factory?.Dispose();
        _factory = null;
        _loadedModelPath = null;
    }

    public async Task<string> TranscribeAsync(string wavFilePath, CancellationToken ct = default)
    {
        var modelPath = await _modelService.EnsureModelExistsAsync(ct);
        if (_loadedModelPath != modelPath)
        {
            UnloadModel();

            _loadedModelPath = modelPath;
            _factory = WhisperFactory.FromPath(modelPath);
            _processor = _factory.CreateBuilder().WithLanguage("auto").Build();
        }

        await using var fileStream = File.OpenRead(wavFilePath);
        var segments = new List<string>();

        await foreach (var segment in _processor!.ProcessAsync(fileStream, ct))
        {
            segments.Add(segment.Text);
        }

        return string.Join(" ", segments).Trim();
    }

    public ValueTask DisposeAsync()
    {
        UnloadModel();
        return ValueTask.CompletedTask;
    }
}