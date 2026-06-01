using System.Diagnostics.CodeAnalysis;
using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Models.Config;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Bulk.Services;

internal static class BulkRunnerExecution
{
    public static bool TryResolveOrigin(
        ILogger logger,
        IBulkSourceRouteService bulkSourceRouteService,
        [NotNullWhen(true)] out string? origin
    )
    {
        origin = bulkSourceRouteService.GetOrigin(BulkSourceType.Media);

        if (origin is not null)
            return true;

        logger.LogInformation("origin is null");
        return false;
    }

    public static BulkPhase1Options CreatePhase1Options(AppConfig config) =>
        new()
        {
            UsersPerCycle = config.Bulk.UsersPerCycle,
            SavePerAction = config.Bulk.SavePerAction,
            ApiPerCycle = config.Bulk.ApiPerCycle,
            MediaPerApi = config.Bulk.MediaPerApi,
            MaxCountPost = config.Bulk.MaxCountPost,
            ApiRetryCount = config.Bulk.ApiRetryCount,
        };

    public static BulkPhase2Options CreatePhase2Options(AppConfig config) =>
        new()
        {
            UsersPerPhase2 = config.Bulk.UsersPerPhase2,
            SavePerAction = config.Bulk.SavePerAction,
            MediaPerApi = config.Bulk.MediaPerApi,
            MaxCountPostPhase2 = config.Bulk.MaxCountPostPhase2,
            ApiRetryCount = config.Bulk.ApiRetryCount,
        };

    public static BulkImportOptions CreateImportOptions(AppConfig config) =>
        new() { UsersPerCycle = config.Bulk.UsersPerCycle };
}
