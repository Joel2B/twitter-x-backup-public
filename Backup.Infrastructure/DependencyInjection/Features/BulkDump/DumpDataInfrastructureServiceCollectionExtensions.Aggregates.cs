using Backup.Infrastructure.Dump.Data;
using Backup.Infrastructure.Interfaces.Data.Dump;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static partial class DumpDataInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection RegisterDumpDataAggregates(this IServiceCollection services)
    {
        services.AddScoped<IDumpsData, DumpsDataMultiStore>();
        services.AddScoped<IDumpData, DumpDataMultiStore>();
        return services;
    }
}
