using Backup.Infrastructure.Interfaces.Services.UtilsService;
using Backup.Infrastructure.Services.UtilsService;
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

