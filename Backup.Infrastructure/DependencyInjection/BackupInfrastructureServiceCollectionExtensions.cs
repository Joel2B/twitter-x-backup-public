using Backup.App.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class BackupInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBackupApiInfrastructure(this IServiceCollection services)
    {
        services.AddCore();
        services.AddSerilog();
        services.AddPostData();
        services.AddPost();
        services.AddSetup();
        return services;
    }
}
