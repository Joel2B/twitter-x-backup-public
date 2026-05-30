using Backup.Infrastructure.Bulk.Adapters;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Runtime;

public static class RuntimeInfrastructureBulkServiceCollectionExtensions
{
    public static IServiceCollection AddBulkRuntimeInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IBulkRequestFactory, BulkRequestFactory>();
        services.AddScoped<IBulkSourceRouteProvider, BulkSourceRouteProvider>();
        services.AddScoped<IBulkApiClient, BulkApiClient>();
        services.AddScoped<IBulkImportRunner, BulkImportRunner>();
        services.AddScoped<IBulkVerifyRunner, BulkVerifyRunner>();
        services.AddScoped<IBulkPhase1Runner, BulkPhase1Runner>();
        services.AddScoped<IBulkPhase2Runner, BulkPhase2Runner>();
        services.AddScoped<IBulkPhase2ResetRunner, BulkPhase2ResetRunner>();
        services.AddScoped<IBulkService, BulkService>();
        return services;
    }
}
