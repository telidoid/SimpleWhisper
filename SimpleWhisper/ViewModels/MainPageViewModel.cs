using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleWhisper.Services;

namespace SimpleWhisper.ViewModels;

public partial class MainPageViewModel : ViewModelBase
{
    private readonly IAudioRecordingService _audioService;
    private readonly IWhisperTranscriptionService _whisperService;
    private readonly IModelDownloadService _modelService;

    [ObservableProperty] private string _transcribedText = string.Empty;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _isTranscribing;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private bool _isDownloadingModel;

    public string RecordButtonText => IsRecording ? "Stop Recording" : "Start Recording";

    public MainPageViewModel(
        IAudioRecordingService audioService,
        IWhisperTranscriptionService whisperService,
        IModelDownloadService modelService)
    {
        _audioService = audioService;
        _whisperService = whisperService;
        _modelService = modelService;

        _modelService.DownloadProgressChanged += p =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => DownloadProgress = p * 100);
    }

    partial void OnIsRecordingChanged(bool value)
    {
        OnPropertyChanged(nameof(RecordButtonText));
    }

    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
        {
            await StopAndTranscribeAsync();
        }
        else
        {
            await StartRecordingAsync();
        }
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            IsDownloadingModel = true;
            StatusMessage = "Checking model...";
            await _modelService.EnsureModelExistsAsync();
            IsDownloadingModel = false;

            StatusMessage = "Recording... Click to stop.";
            await _audioService.StartRecordingAsync();
            IsRecording = true;
        }
        catch (Exception ex)
        {
            IsDownloadingModel = false;
            IsRecording = false;
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task StopAndTranscribeAsync()
    {
        try
        {
            StatusMessage = "Stopping recording...";
            var wavPath = await _audioService.StopRecordingAsync();
            IsRecording = false;

            StatusMessage = "Transcribing...";
            IsTranscribing = true;

            var text = await _whisperService.TranscribeAsync(wavPath);

            if (!string.IsNullOrWhiteSpace(text))
                TranscribedText += (TranscribedText.Length > 0 ? " " : "") + text;

            StatusMessage = "Ready";

            try { File.Delete(wavPath); }
            catch { /* ignored */ }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsRecording = false;
            IsTranscribing = false;
        }
    }
}
