using Microsoft.Extensions.DependencyInjection;
using SimpleWhisper.Core.Services;

namespace SimpleWhisper.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSimpleWhisperCore(this IServiceCollection services)
    {
        services.AddSingleton<IModelSelectionService, ModelSelectionService>();
        services.AddSingleton<IModelDownloadService, ModelDownloadService>();
        services.AddSingleton<IModelCatalogService, ModelCatalogService>();
        services.AddSingleton<IWhisperTranscriptionService, WhisperTranscriptionService>();
        return services;
    }
}
