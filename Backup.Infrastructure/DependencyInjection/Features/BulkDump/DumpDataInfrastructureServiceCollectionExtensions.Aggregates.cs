using Backup.Infrastructure.Dump.Data;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Features.BulkDump;

public static partial class DumpDataInfrastructureServiceCollectionExtensions
{
    private static IServiceCollection RegisterDumpDataAggregates(this IServiceCollection services)
    {
        services.AddScoped<IDumpsData, DumpsDataMultiStore>();
        services.AddScoped<IDumpData, DumpDataMultiStore>();
        return services;
    }
}
