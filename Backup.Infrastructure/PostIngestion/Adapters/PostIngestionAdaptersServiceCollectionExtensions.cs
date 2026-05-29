using Backup.Application.PostIngestion.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.PostIngestion.Adapters;

public static class PostIngestionAdaptersServiceCollectionExtensions
{
    public static IServiceCollection AddPostIngestionAdapters(this IServiceCollection services)
    {
        services.AddScoped<IRawPostParser, RawPostParserAdapter>();
        services.AddScoped<IPostStoreWriter, PostStoreWriterAdapter>();
        return services;
    }
}
