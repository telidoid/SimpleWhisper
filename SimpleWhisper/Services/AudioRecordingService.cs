using System.Runtime.InteropServices;
using PortAudioSharp;
using Stream = PortAudioSharp.Stream;

namespace SimpleWhisper.Services;

public class AudioRecordingService : IAudioRecordingService, IDisposable
{
    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;
    private const int BytesPerSample = BitsPerSample / 8;
    private const int WavHeaderSize = 44;

    private readonly object _lock = new();

    private FileStream? _fileStream;
    private Stream? _paStream;
    private string? _currentFilePath;
    private long _totalDataBytes;
    private bool _isRecording;
    private bool _initialized;
    private Exception? _callbackError;
    private byte[]? _callbackBuffer;

    public bool IsRecording => _isRecording;

    public Task<string> StartRecordingAsync(CancellationToken ct = default)
    {
        if (_isRecording)
        {
            StopAndCleanup();
        }

        if (!_initialized)
        {
            PortAudio.Initialize();
            _initialized = true;
        }

        var inputDevice = PortAudio.DefaultInputDevice;
        if (inputDevice == PortAudio.NoDevice)
            throw new InvalidOperationException("No audio input device found. Check your microphone setup.");

        var deviceInfo = PortAudio.GetDeviceInfo(inputDevice);

        var filePath = Path.Combine(Path.GetTempPath(), $"simplewhisper_{Guid.NewGuid()}.wav");
        _currentFilePath = filePath;
        _totalDataBytes = 0;
        _callbackError = null;

        _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        WriteWavHeader(_fileStream);

        try
        {
            var inputParams = new StreamParameters
            {
                device = inputDevice,
                channelCount = Channels,
                sampleFormat = SampleFormat.Int16,
                suggestedLatency = deviceInfo.defaultLowInputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            _paStream = new Stream(
                inParams: inputParams,
                outParams: null,
                sampleRate: SampleRate,
                framesPerBuffer: PortAudio.FramesPerBufferUnspecified,
                streamFlags: StreamFlags.ClipOff,
                callback: AudioCallback,
                userData: this
            );

            _paStream.Start();
            _isRecording = true;
        }
        catch
        {
            _fileStream.Dispose();
            _fileStream = null;
            try { File.Delete(filePath); } catch { /* ignored */ }
            _currentFilePath = null;
            throw;
        }

        return Task.FromResult(filePath);
    }

    public Task<string> StopRecordingAsync()
    {
        if (!_isRecording || _paStream is null)
            throw new InvalidOperationException("Not currently recording");

        var filePath = _currentFilePath!;

        _paStream.Stop();
        _paStream.Dispose();
        _paStream = null;
        _isRecording = false;

        lock (_lock)
        {
            FinalizeWavHeader(_fileStream!);
            _fileStream!.Dispose();
            _fileStream = null;
        }

        _currentFilePath = null;

        if (_callbackError is not null)
        {
            var ex = _callbackError;
            _callbackError = null;
            try { File.Delete(filePath); } catch { /* ignored */ }
            throw new InvalidOperationException("Recording failed due to an audio error.", ex);
        }

        return Task.FromResult(filePath);
    }

    private static StreamCallbackResult AudioCallback(
        nint input, nint output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags, nint userDataPtr)
    {
        var self = Stream.GetUserData<AudioRecordingService>(userDataPtr);

        var byteCount = (int)(frameCount * Channels * BytesPerSample);
        if (self._callbackBuffer is null || self._callbackBuffer.Length < byteCount)
            self._callbackBuffer = new byte[byteCount];
        Marshal.Copy(input, self._callbackBuffer, 0, byteCount);

        lock (self._lock)
        {
            try
            {
                self._fileStream?.Write(self._callbackBuffer, 0, byteCount);
                self._totalDataBytes += byteCount;
            }
            catch (Exception ex)
            {
                self._callbackError = ex;
                return StreamCallbackResult.Abort;
            }
        }

        return StreamCallbackResult.Continue;
    }

    private static void WriteWavHeader(FileStream fs)
    {
        using var writer = new BinaryWriter(fs, System.Text.Encoding.ASCII, leaveOpen: true);

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(36u); // placeholder for file size - 8
        writer.Write("WAVE"u8);

        // fmt subchunk
        writer.Write("fmt "u8);
        writer.Write(16u); // subchunk size (PCM)
        writer.Write((ushort)1); // audio format (PCM)
        writer.Write((ushort)Channels);
        writer.Write((uint)SampleRate);
        writer.Write((uint)(SampleRate * Channels * BytesPerSample)); // byte rate
        writer.Write((ushort)(Channels * BytesPerSample)); // block align
        writer.Write((ushort)BitsPerSample);

        // data subchunk
        writer.Write("data"u8);
        writer.Write(0u); // placeholder for data size
    }

    private void FinalizeWavHeader(FileStream fs)
    {
        var dataSize = (uint)_totalDataBytes;

        fs.Seek(4, SeekOrigin.Begin);
        WriteUInt32(fs, dataSize + 36);

        fs.Seek(40, SeekOrigin.Begin);
        WriteUInt32(fs, dataSize);

        fs.Flush();
    }

    private static void WriteUInt32(FileStream fs, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        fs.Write(buf);
    }

    private void StopAndCleanup()
    {
        if (_paStream is not null)
        {
            try { _paStream.Stop(); } catch { /* ignored */ }
            _paStream.Dispose();
            _paStream = null;
        }

        _fileStream?.Dispose();
        _fileStream = null;
        _isRecording = false;
    }

    public void Dispose()
    {
        StopAndCleanup();

        if (_initialized)
        {
            try { PortAudio.Terminate(); } catch { /* ignored */ }
            _initialized = false;
        }
    }
}
