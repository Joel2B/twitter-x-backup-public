using Backup.Infrastructure.Bulk.Data;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static partial class BulkDataInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection RegisterBulkDataAggregates(this IServiceCollection services)
    {
        services.AddScoped<IBulkSourceData, BulkSourceDataMultiStore>();
        services.AddScoped<IBulkData, BulkDataMultiStore>();
        return services;
    }
}
