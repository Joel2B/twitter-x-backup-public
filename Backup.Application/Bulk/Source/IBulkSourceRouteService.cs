using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public interface IBulkSourceRouteService
{
    string? GetOrigin(BulkSourceType sourceType);
    string GetReferer(BulkSourceType sourceType, string? userName = null);
}
