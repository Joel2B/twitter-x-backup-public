using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.BulkDump;

public static partial class DumpDataInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDumpDataInfrastructure(this IServiceCollection services)
    {
        services.RegisterDumpDataStores();
        services.RegisterDumpDataAggregates();
        return services;
    }
}
