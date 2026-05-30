using Backup.Infrastructure.Interfaces.Services.Bulk;
using Backup.Infrastructure.Models.Bulk;

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
