using Microsoft.Extensions.DependencyInjection;
using SimpleWhisper.Audio.PortAudio.Services;

namespace SimpleWhisper.Audio.PortAudio.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPortAudioRecording(this IServiceCollection services)
    {
        services.AddSingleton<IAudioRecordingService, AudioRecordingService>();
        services.AddSingleton<IInputDeviceService, InputDeviceService>();
        return services;
    }
}
