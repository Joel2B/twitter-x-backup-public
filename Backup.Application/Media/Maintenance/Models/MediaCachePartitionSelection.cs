namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCachePartitionSelection
{
    public bool UseHeavyPartition { get; init; }
    public int? PreferredPartitionId { get; init; }
    public long RequestedSizeBytes { get; init; }
}
