using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Api.Services;

public static class PostIngestionServiceCollectionExtensions
{
    public static IServiceCollection AddPostIngestionApi(this IServiceCollection services)
    {
        services.AddScoped<IPostIngestionService, PostIngestionService>();
        return services;
    }
}
