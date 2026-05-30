using Backup.Infrastructure.Models.Bulk;

namespace Backup.Infrastructure.Interfaces.Services.Bulk;

public interface IBulkSourceRouteProvider
{
    string? GetOrigin(SourceType sourceType);
    string GetReferer(SourceType sourceType = SourceType.Notifications, string? userName = null);
}
