using Microsoft.Extensions.DependencyInjection;

namespace Backup.Api.Services;

public static class OperationsApiServiceCollectionExtensions
{
    public static IServiceCollection AddOperationsApi(this IServiceCollection services)
    {
        services.AddScoped<ConfigContextResolver>();
        services.AddScoped<BackupOperationsService>();
        services.AddScoped<BulkOperationsService>();
        services.AddScoped<ConfigOperationsService>();
        services.AddScoped<MediaOperationsService>();
        services.AddScoped<MediaQueryService>();
        services.AddScoped<PartitionOperationsService>();
        services.AddScoped<PostOperationsService>();
        services.AddScoped<PostQueryService>();
        return services;
    }
}
