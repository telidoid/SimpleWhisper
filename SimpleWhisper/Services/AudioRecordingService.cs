using System.Diagnostics;

namespace SimpleWhisper.Services;

public class AudioRecordingService : IAudioRecordingService, IDisposable
{
    private Process? _ffmpegProcess;
    private string? _currentFilePath;

    public bool IsRecording => _ffmpegProcess is { HasExited: false };

    public Task<string> StartRecordingAsync(CancellationToken ct = default)
    {
        if (IsRecording)
        {
            _ffmpegProcess!.Kill();
            _ffmpegProcess.Dispose();
            _ffmpegProcess = null;
        }

        var filePath = Path.Combine(Path.GetTempPath(), $"simplewhisper_{Guid.NewGuid()}.wav");
        _currentFilePath = filePath;

        var (format, input) = true switch
        {
            _ when OperatingSystem.IsWindows() => ("dshow", "audio=Microphone"),
            _ when OperatingSystem.IsMacOS()   => ("avfoundation", ":default"),
            _                                   => ("pulse", "default"),
        };

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-f {format} -i \"{input}\" -ac 1 -ar 16000 -sample_fmt s16 -y \"{filePath}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _ffmpegProcess = Process.Start(psi)
                         ?? throw new InvalidOperationException(
                             "Failed to start ffmpeg. Ensure ffmpeg is installed and available on PATH.");

        // Brief delay to check if ffmpeg started successfully
        Task.Delay(500, ct).ContinueWith(_ =>
        {
            if (_ffmpegProcess is { HasExited: true, ExitCode: not 0 })
                throw new InvalidOperationException(
                    "ffmpeg failed to start recording. Check your microphone and audio setup.");
        }, ct);

        return Task.FromResult(filePath);
    }

    public async Task<string> StopRecordingAsync()
    {
        if (_ffmpegProcess is null || _ffmpegProcess.HasExited)
            throw new InvalidOperationException("Not currently recording");

        var filePath = _currentFilePath!;

        // Send 'q' to ffmpeg stdin for graceful shutdown (finalizes WAV header)
        await _ffmpegProcess.StandardInput.WriteAsync("q");
        await _ffmpegProcess.StandardInput.FlushAsync();

        await _ffmpegProcess.WaitForExitAsync();
        _ffmpegProcess.Dispose();
        _ffmpegProcess = null;
        _currentFilePath = null;

        return filePath;
    }

    public void Dispose()
    {
        if (_ffmpegProcess is { HasExited: false })
        {
            _ffmpegProcess.Kill();
        }
        _ffmpegProcess?.Dispose();
        _ffmpegProcess = null;
    }
}
