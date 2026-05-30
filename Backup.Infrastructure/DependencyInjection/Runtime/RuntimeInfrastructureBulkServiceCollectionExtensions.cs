using Backup.Application.Bulk;
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
        services.AddScoped<IBulkImportService, BulkImportService>();
        services.AddScoped<IBulkVerifyService, BulkVerifyService>();
        services.AddScoped<IBulkPhase2ResetService, BulkPhase2ResetService>();
        services.AddScoped<IBulkPhase1Service, BulkPhase1Service>();
        services.AddScoped<IBulkPhase2Service, BulkPhase2Service>();
        services.AddScoped<IBulkPhase1Runner, BulkPhase1Runner>();
        services.AddScoped<IBulkPhase2Runner, BulkPhase2Runner>();
        services.AddScoped<IBulkPhase2ResetRunner, BulkPhase2ResetRunner>();
        services.AddScoped<IBulkExecutionService, BulkExecutionService>();
        return services;
    }
}
