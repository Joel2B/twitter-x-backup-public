using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class BulkDumpInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBulkInfrastructure(this IServiceCollection services)
    {
        services.AddBulkDataInfrastructure();
        return services;
    }

    public static IServiceCollection AddDumpInfrastructure(this IServiceCollection services)
    {
        services.AddDumpDataInfrastructure();
        return services;
    }
}
