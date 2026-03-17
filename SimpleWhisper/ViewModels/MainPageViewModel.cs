using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
    private readonly ITextPasteService _textPasteService;
    private readonly IAppSettingsService _appSettings;

    [ObservableProperty] private string _transcribedText = string.Empty;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _isTranscribing;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private bool _isDownloadingModel;
    [ObservableProperty] private bool _isBusy;
    private CancellationTokenSource? _transcriptionCts;

    public string RecordButtonText => IsTranscribing ? "Cancel" : IsRecording ? "Stop Recording" : "Start Recording";

    public MainPageViewModel(
        IAudioRecordingService audioService,
        IWhisperTranscriptionService whisperService,
        IModelDownloadService modelService,
        IGlobalHotkeyService hotkeyService,
        INotificationService notificationService,
        ITextPasteService textPasteService,
        IAppSettingsService appSettings)
    {
        _audioService = audioService;
        _whisperService = whisperService;
        _modelService = modelService;
        _notificationService = notificationService;
        _textPasteService = textPasteService;
        _appSettings = appSettings;

        _modelService.DownloadProgressChanged += p =>
            Dispatcher.UIThread.Post(() => DownloadProgress = p * 100);

        hotkeyService.RecordingStartRequested += (_, _) =>
            Dispatcher.UIThread.Post(async void () =>
            {
                if (IsBusy && !IsTranscribing) return;
                if (_appSettings.RecordingMode == RecordingMode.Toggle)
                    await ToggleRecordingAsync();
                else if (!IsRecording)
                    await ToggleRecordingAsync();
            });

        hotkeyService.RecordingStopRequested += (_, _) =>
            Dispatcher.UIThread.Post(async void () =>
            {
                if (IsBusy && !IsTranscribing) return;
                if (_appSettings.RecordingMode == RecordingMode.Hold && IsRecording)
                    await ToggleRecordingAsync();
            });
    }

    partial void OnIsRecordingChanged(bool value)
    {
        OnPropertyChanged(nameof(RecordButtonText));
    }

    partial void OnIsTranscribingChanged(bool value)
    {
        OnPropertyChanged(nameof(RecordButtonText));
    }

    partial void OnIsBusyChanged(bool value)
    {
        ToggleRecordingCommand.NotifyCanExecuteChanged();
    }

    private bool CanToggleRecording() => !IsBusy || IsTranscribing;

    [RelayCommand(CanExecute = nameof(CanToggleRecording))]
    private async Task ToggleRecordingAsync()
    {
        if (IsTranscribing)
        {
            _transcriptionCts?.Cancel();
            return;
        }

        IsBusy = true;
        try
        {
            if (IsRecording)
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
            IsBusy = false;

            _transcriptionCts = new CancellationTokenSource();
            var text = await _whisperService.TranscribeAsync(wavPath, _transcriptionCts.Token);

            if (!string.IsNullOrWhiteSpace(text))
            {
                TranscribedText += (TranscribedText.Length > 0 ? " " : "") + text;
                if (_appSettings.ShowNotification)
                    _ = _notificationService.NotifyAsync(text);

                if (_appSettings.CopyToClipboard)
                {
                    var clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;
                    if (clipboard != null)
                        await clipboard.SetTextAsync(text);
                }

                if (_appSettings.PasteIntoFocusedWindow && _textPasteService.IsAvailable)
                {
                    await Task.Delay(300);
                    await _textPasteService.PasteAsync(text);
                }
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
            IsRecording = false;
            IsTranscribing = false;
        }
    }
}