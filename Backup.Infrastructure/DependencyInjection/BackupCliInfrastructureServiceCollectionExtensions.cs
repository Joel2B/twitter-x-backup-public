using Backup.Infrastructure.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class BackupCliInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBackupCliInfrastructure(this IServiceCollection services)
    {
        services.AddInfrastructureBase();
        services.AddBackupCliFeatureSet();
        services.AddScoped<IBackupCliRunner, BackupCliRunner>();

        return services;
    }
}
