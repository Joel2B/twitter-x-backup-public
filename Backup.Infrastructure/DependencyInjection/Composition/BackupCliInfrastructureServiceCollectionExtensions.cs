using Backup.Infrastructure.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Composition;

public static class BackupCliInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBackupCliInfrastructure(this IServiceCollection services)
    {
        services.AddInfrastructureBase("cli", 30);
        services.AddBackupCliFeatureSet();
        services.AddScoped<BackupCliRunner>();

        return services;
    }
}
