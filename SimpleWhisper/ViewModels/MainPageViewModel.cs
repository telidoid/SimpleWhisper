using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleWhisper.Services;
using SimpleWhisper.Services.Hotkey;

namespace SimpleWhisper.ViewModels;

public partial class MainPageViewModel : ViewModelBase
{
    private readonly IAudioRecordingService _audioService;
    private readonly IWhisperTranscriptionService _whisperService;
    private readonly IModelDownloadService _modelService;
    private readonly INotificationService _notificationService;

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
        IModelDownloadService modelService,
        IGlobalHotkeyService hotkeyService,
        INotificationService notificationService)
    {
        _audioService = audioService;
        _whisperService = whisperService;
        _modelService = modelService;
        _notificationService = notificationService;

        _modelService.DownloadProgressChanged += p =>
            Dispatcher.UIThread.Post(() => DownloadProgress = p * 100);

        hotkeyService.RecordingStartRequested += (_, _) =>
            Dispatcher.UIThread.Post(async void () =>
            {
                if (!IsRecording) await StartRecordingAsync();
            });

        hotkeyService.RecordingStopRequested += (_, _) =>
            Dispatcher.UIThread.Post(async void () =>
            {
                if (IsRecording) await StopAndTranscribeAsync();
            });
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
            {
                TranscribedText += (TranscribedText.Length > 0 ? " " : "") + text;
                _ = _notificationService.NotifyAsync(text);
            }

            StatusMessage = "Ready";

            try
            {
                File.Delete(wavPath);
            }
            catch
            {
                /* ignored */
            }
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