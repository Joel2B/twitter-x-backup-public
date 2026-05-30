using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static partial class PostDataInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPostDataInfrastructure(this IServiceCollection services)
    {
        services.RegisterPostDataStores();
        services.RegisterPostDataAggregates();
        return services;
    }
}



