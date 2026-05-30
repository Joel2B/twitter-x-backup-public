using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Abstractions.Services;

public interface IBulkSourceRouteProvider
{
    string? GetOrigin(SourceType sourceType);
    string GetReferer(SourceType sourceType = SourceType.Notifications, string? userName = null);
}
