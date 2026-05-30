using Backup.Infrastructure.Data.Media;
using Backup.Infrastructure.Interfaces;
using Backup.Infrastructure.Media.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class RuntimeInfrastructureSetupMediaLoggerServiceCollectionExtensions
{
    public static IServiceCollection AddMediaLoggerSetupInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<LocalMediaLogger>();
        services.AddScoped<IMediaLogger>(sp => sp.GetRequiredService<LocalMediaLogger>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<LocalMediaLogger>());
        return services;
    }
}
