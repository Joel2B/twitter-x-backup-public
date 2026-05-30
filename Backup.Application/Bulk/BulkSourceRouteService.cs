using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public sealed class BulkSourceRouteService : IBulkSourceRouteService
{
    public string? GetOrigin(BulkSourceType sourceType) =>
        sourceType switch
        {
            BulkSourceType.Media => "media",
            BulkSourceType.Notifications => "notifications",
            _ => null,
        };

    public string GetReferer(BulkSourceType sourceType, string? userName = null)
    {
        const string baseUrl = "https://x.com";
        string? origin = GetOrigin(sourceType);

        if (string.IsNullOrWhiteSpace(origin))
            return $"{baseUrl}/";

        if (string.IsNullOrWhiteSpace(userName))
            return $"{baseUrl}/{origin}";

        return $"{baseUrl}/{userName}/{origin}";
    }
}
