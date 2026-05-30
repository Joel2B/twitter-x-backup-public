using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Adapters;

public sealed class BulkSourceRouteProvider : IBulkSourceRouteProvider
{
    public string? GetOrigin(SourceType sourceType) =>
        sourceType switch
        {
            SourceType.Media => "media",
            SourceType.Notifications => "notifications",
            _ => null,
        };

    public string GetReferer(
        SourceType sourceType = SourceType.Notifications,
        string? userName = null
    )
    {
        string baseUrl = "https://x.com/";
        string? origin = GetOrigin(sourceType);
        string url = "{origin}";

        if (userName is not null)
            url = "{userName}/{origin}";

        url = url.Replace("{userName}", userName).Replace("{origin}", origin);
        return $"{baseUrl}{url}";
    }
}
