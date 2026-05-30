using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static partial class BulkDataInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBulkDataInfrastructure(this IServiceCollection services)
    {
        services.RegisterBulkDataStores();
        services.RegisterBulkDataAggregates();
        return services;
    }
}



