using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Adapters;

public sealed class BulkSourceRouteProvider(IBulkSourceRouteService routeService) : IBulkSourceRouteProvider
{
    private readonly IBulkSourceRouteService _routeService = routeService;

    public string? GetOrigin(SourceType sourceType) => _routeService.GetOrigin(ToApplication(sourceType));

    public string GetReferer(
        SourceType sourceType = SourceType.Notifications,
        string? userName = null
    ) => _routeService.GetReferer(ToApplication(sourceType), userName);

    private static BulkSourceType ToApplication(SourceType sourceType) =>
        sourceType switch
        {
            SourceType.None => BulkSourceType.None,
            SourceType.Media => BulkSourceType.Media,
            SourceType.Status => BulkSourceType.Status,
            SourceType.Notifications => BulkSourceType.Notifications,
            _ => BulkSourceType.None,
        };
}
