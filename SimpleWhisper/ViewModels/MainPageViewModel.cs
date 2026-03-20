using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleWhisper.Resources;
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
    private readonly IInputDeviceService _inputDeviceService;
    private readonly IWhisperTranscriptionService _whisperService;
    private readonly IModelDownloadService _modelService;
    private readonly INotificationService _notificationService;
    private readonly ITextPasteService _textPasteService;
    private readonly IClipboardService _clipboardService;
    private readonly IAppSettingsService _appSettings;

    [ObservableProperty] private string _transcribedText = string.Empty;
    [ObservableProperty] private AppState _appState;
    [ObservableProperty] private string _statusMessage = Strings.StatusReady;
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private bool _isDownloadingModel;
    [ObservableProperty] private bool _isBusy;
    private CancellationTokenSource? _transcriptionCts;

    public string RecordButtonText => AppState switch
    {
        AppState.Transcribing => Strings.RecordButtonCancel,
        AppState.Recording => Strings.RecordButtonStop,
        _ => Strings.RecordButtonStart
    };

    public MainPageViewModel(
        IAudioRecordingService audioService,
        IInputDeviceService inputDeviceService,
        IWhisperTranscriptionService whisperService,
        IModelDownloadService modelService,
        IGlobalHotkeyService hotkeyService,
        INotificationService notificationService,
        ITextPasteService textPasteService,
        IClipboardService clipboardService,
        IAppSettingsService appSettings)
    {
        _audioService = audioService;
        _inputDeviceService = inputDeviceService;
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

    [RelayCommand]
    private void ClearText() => TranscribedText = string.Empty;

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
            StatusMessage = Strings.StatusCheckingModel;
            await _modelService.EnsureModelExistsAsync();
            IsDownloadingModel = false;

            StatusMessage = Strings.StatusRecording;
            _inputDeviceService.ActivateDevice(_appSettings.SelectedInputDeviceName);
            await _audioService.StartRecordingAsync();
            AppState = AppState.Recording;
        }
        catch (Exception ex)
        {
            IsDownloadingModel = false;
            _inputDeviceService.RestoreDefaultDevice();
            AppState = AppState.Idle;
            StatusMessage = string.Format(Strings.StatusError, ex.Message);
        }
    }

    private async Task StopAndTranscribeAsync()
    {
        try
        {
            StatusMessage = Strings.StatusStopping;
            var wavPath = await _audioService.StopRecordingAsync();

            StatusMessage = Strings.StatusTranscribing;
            AppState = AppState.Transcribing;
            IsBusy = false;

            _transcriptionCts = new CancellationTokenSource();
            var text = await _whisperService.TranscribeAsync(wavPath, _transcriptionCts.Token);

            if (!string.IsNullOrWhiteSpace(text))
            {
                TranscribedText += (TranscribedText.Length > 0 ? Environment.NewLine : "") + text;
                
                if (_appSettings.ShowNotification)
                    _ = _notificationService.NotifyAsync(text);

                if (_appSettings.CopyToClipboard)
                    await _clipboardService.SetTextAsync(text);

                if (_appSettings.PasteIntoFocusedWindow && _textPasteService.IsAvailable)
                    await _textPasteService.PasteAsync(text);
            }

            StatusMessage = Strings.StatusReady;

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
            StatusMessage = Strings.StatusCancelled;
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Strings.StatusError, ex.Message);
        }
        finally
        {
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
            if (_audioService.IsRecording)
            {
                try { await _audioService.StopRecordingAsync(); } catch { /* ensure cleanup */ }
            }
            _inputDeviceService.RestoreDefaultDevice();
            AppState = AppState.Idle;
        }
    }
}