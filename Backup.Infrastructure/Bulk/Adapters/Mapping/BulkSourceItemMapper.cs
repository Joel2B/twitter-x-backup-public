using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Adapters;

internal static class BulkSourceItemMapper
{
    public static BulkSourceItem ToApplication(Source source) =>
        new() { UserName = source.UserName, Type = ToApplication(source.Type) };

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
