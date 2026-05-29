using Backup.Infrastructure.PostIngestion.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Api.Services;

public static class PostIngestionServiceCollectionExtensions
{
    public static IServiceCollection AddPostIngestionApi(this IServiceCollection services)
    {
        services.AddPostIngestionAdapters();
        services.AddScoped<
            Backup.Application.PostIngestion.IPostIngestionService,
            Backup.Application.PostIngestion.PostIngestionService
        >();
        services.AddScoped<IPostIngestionService, PostIngestionService>();
        return services;
    }
}
