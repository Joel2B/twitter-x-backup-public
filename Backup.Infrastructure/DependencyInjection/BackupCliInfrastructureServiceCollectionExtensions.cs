using Backup.App.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class BackupCliInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBackupCliInfrastructure(this IServiceCollection services)
    {
        services.AddCoreInfrastructure();
        services.AddSerilog();

        services.AddPostData();
        services.AddDumpData();
        services.AddBulkData();
        services.AddMediaData();

        services.AddUtils();
        services.AddPost();
        services.AddMedia();
        services.AddMediaBackup();
        services.AddServices();

        services.AddSetup();
        services.AddApp();

        return services;
    }
}
