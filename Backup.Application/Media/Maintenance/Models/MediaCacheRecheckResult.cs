namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheRecheckResult
{
    public bool IsInvalid { get; init; }
    public bool ShouldRemove { get; init; }
    public bool ShouldUpdate { get; init; }
    public int? PartitionId { get; init; }
    public long? StreamSizeBytes { get; init; }
    public long? FileSizeBytes { get; init; }
}
