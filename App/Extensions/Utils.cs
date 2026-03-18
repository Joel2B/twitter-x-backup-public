using Backup.App.Interfaces.Services.UtilsService;
using Backup.App.Services.UtilsService;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class UtilsCollectionExtensions
{
    public static IServiceCollection AddUtils(this IServiceCollection services)
    {
        services.AddSingleton<IZipWriterFactory, ZipWriterFactory>();
        services.AddSingleton<IBandwidthLimiter, BandwidthLimiter>();

        return services;
    }
}
