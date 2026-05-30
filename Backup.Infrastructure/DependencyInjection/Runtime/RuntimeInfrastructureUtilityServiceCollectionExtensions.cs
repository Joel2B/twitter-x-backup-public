using Backup.Infrastructure.Utility.Abstractions.Services;
using Backup.Infrastructure.Services.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class RuntimeInfrastructureUtilityServiceCollectionExtensions
{
    public static IServiceCollection AddUtilityRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IZipWriterFactory, ZipWriterFactory>();
        services.AddSingleton<IBandwidthLimiter, BandwidthLimiter>();
        return services;
    }
}
