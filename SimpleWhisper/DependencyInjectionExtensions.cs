using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleWhisper.Services;
using SimpleWhisper.ViewModels;

namespace SimpleWhisper;

internal static class DependencyInjectionExtensions
{
    public static IHostBuilder ConfigureSimpleWhisper(this IHostBuilder builder)
        => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IModelSelectionService, ModelSelectionService>();
            services.AddSingleton<IModelDownloadService, ModelDownloadService>();
            services.AddSingleton<IWhisperTranscriptionService, WhisperTranscriptionService>();
            services.AddTransient<IAudioRecordingService, AudioRecordingService>();
            services.AddSingleton<MainPageViewModel>();
            services.AddSingleton<ModelsPageViewModel>();
            services.AddSingleton<MainWindowViewModel>();
        });
}
