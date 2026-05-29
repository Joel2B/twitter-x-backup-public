using Backup.App.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class BackupCliInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBackupCliInfrastructure(this IServiceCollection services)
    {
        services.AddCoreInfrastructure();
        services.AddSerilog();
        services.AddPostsInfrastructure();
        services.AddDumpInfrastructure();
        services.AddBulkInfrastructure();
        services.AddMediaInfrastructure();
        services.AddRuntimeServicesInfrastructure();
        services.AddSetupInfrastructure();
        services.AddAppRuntimeInfrastructure();

        return services;
    }
}
