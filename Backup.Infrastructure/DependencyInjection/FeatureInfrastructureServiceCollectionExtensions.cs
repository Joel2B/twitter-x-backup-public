using Backup.App.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class FeatureInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPostsInfrastructure(this IServiceCollection services)
    {
        services.AddPostData();
        services.AddPost();
        return services;
    }

    public static IServiceCollection AddMediaInfrastructure(this IServiceCollection services)
    {
        services.AddMediaData();
        services.AddMedia();
        services.AddMediaBackup();
        return services;
    }

    public static IServiceCollection AddBulkInfrastructure(this IServiceCollection services)
    {
        services.AddBulkData();
        return services;
    }

    public static IServiceCollection AddDumpInfrastructure(this IServiceCollection services)
    {
        services.AddDumpData();
        return services;
    }

    public static IServiceCollection AddRuntimeServicesInfrastructure(this IServiceCollection services)
    {
        services.AddUtils();
        services.AddServices();
        return services;
    }

    public static IServiceCollection AddSetupInfrastructure(this IServiceCollection services)
    {
        services.AddSetup();
        return services;
    }

    public static IServiceCollection AddAppRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddApp();
        return services;
    }
}
