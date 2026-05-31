namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheWritePlan
{
    public required string CacheKey { get; init; }
    public required MediaCacheEntryState EntryState { get; init; }
}
