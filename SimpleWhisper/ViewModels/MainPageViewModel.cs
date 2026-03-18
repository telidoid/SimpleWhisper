using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleWhisper.Services;
using SimpleWhisper.Services.Hotkey;

namespace SimpleWhisper.ViewModels;

public enum AppState
{
    Idle,
    Recording,
    Transcribing
}

public partial class MainPageViewModel : ViewModelBase
{
    private readonly IAudioRecordingService _audioService;
    private readonly IWhisperTranscriptionService _whisperService;
    private readonly IModelDownloadService _modelService;
    private readonly INotificationService _notificationService;
    private readonly ITextPasteService _textPasteService;
    private readonly IClipboardService _clipboardService;
    private readonly IAppSettingsService _appSettings;

    [ObservableProperty] private string _transcribedText = string.Empty;
    [ObservableProperty] private AppState _appState;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private bool _isDownloadingModel;
    [ObservableProperty] private bool _isBusy;
    private CancellationTokenSource? _transcriptionCts;

    public string RecordButtonText => AppState switch
    {
        AppState.Transcribing => "Cancel",
        AppState.Recording => "Stop Recording",
        _ => "Start Recording"
    };

    public MainPageViewModel(
        IAudioRecordingService audioService,
        IWhisperTranscriptionService whisperService,
        IModelDownloadService modelService,
        IGlobalHotkeyService hotkeyService,
        INotificationService notificationService,
        ITextPasteService textPasteService,
        IClipboardService clipboardService,
        IAppSettingsService appSettings)
    {
        _audioService = audioService;
        _whisperService = whisperService;
        _modelService = modelService;
        _notificationService = notificationService;
        _textPasteService = textPasteService;
        _clipboardService = clipboardService;
        _appSettings = appSettings;

        _modelService.DownloadProgressChanged += p =>
            Dispatcher.UIThread.Post(() => DownloadProgress = p * 100);

        hotkeyService.RecordingStartRequested += (_, _) =>
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var shouldToggle = (_appSettings.RecordingMode, AppState, IsBusy) switch
                {
                    (_, _, true) when AppState != AppState.Transcribing => false,
                    (RecordingMode.Toggle, _, _) => true,
                    (RecordingMode.Hold, AppState.Recording, _) => false,
                    _ => true
                };
                if (shouldToggle) await ToggleRecordingAsync();
            });

        hotkeyService.RecordingStopRequested += (_, _) =>
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var shouldToggle = (_appSettings.RecordingMode, AppState, IsBusy) switch
                {
                    (_, _, true) when AppState != AppState.Transcribing => false,
                    (RecordingMode.Hold, AppState.Recording, _) => true,
                    _ => false
                };
                if (shouldToggle) await ToggleRecordingAsync();
            });
    }

    partial void OnAppStateChanged(AppState value)
    {
        OnPropertyChanged(nameof(RecordButtonText));
    }

    partial void OnIsBusyChanged(bool value)
    {
        ToggleRecordingCommand.NotifyCanExecuteChanged();
    }

    private bool CanToggleRecording() => !IsBusy || AppState == AppState.Transcribing;

    [RelayCommand(CanExecute = nameof(CanToggleRecording))]
    private async Task ToggleRecordingAsync()
    {
        if (AppState == AppState.Transcribing)
        {
            _transcriptionCts?.Cancel();
            return;
        }

        IsBusy = true;
        try
        {
            if (AppState == AppState.Recording)
                await StopAndTranscribeAsync();
            else
                await StartRecordingAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StartRecordingAsync()
    {
        _textPasteService.CaptureTargetWindow();
        try
        {
            IsDownloadingModel = true;
            StatusMessage = "Checking model...";
            await _modelService.EnsureModelExistsAsync();
            IsDownloadingModel = false;

            StatusMessage = "Recording... Click to stop.";
            await _audioService.StartRecordingAsync();
            AppState = AppState.Recording;
        }
        catch (Exception ex)
        {
            IsDownloadingModel = false;
            AppState = AppState.Idle;
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task StopAndTranscribeAsync()
    {
        try
        {
            StatusMessage = "Stopping recording...";
            var wavPath = await _audioService.StopRecordingAsync();

            StatusMessage = "Transcribing...";
            AppState = AppState.Transcribing;
            IsBusy = false;

            _transcriptionCts = new CancellationTokenSource();
            var text = await _whisperService.TranscribeAsync(wavPath, _transcriptionCts.Token);

            if (!string.IsNullOrWhiteSpace(text))
            {
                TranscribedText += (TranscribedText.Length > 0 ? " " : "") + text;
                
                if (_appSettings.ShowNotification)
                    _ = _notificationService.NotifyAsync(text);

                if (_appSettings.CopyToClipboard)
                    await _clipboardService.SetTextAsync(text);

                if (_appSettings.PasteIntoFocusedWindow && _textPasteService.IsAvailable)
                    await _textPasteService.PasteAsync(text);
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
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
            if (_audioService.IsRecording)
            {
                try { await _audioService.StopRecordingAsync(); } catch { /* ensure cleanup */ }
            }
            AppState = AppState.Idle;
        }
    }
}