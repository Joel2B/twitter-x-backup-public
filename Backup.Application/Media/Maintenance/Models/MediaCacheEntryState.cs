namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheEntryState
{
    public required string Path { get; init; }
    public int PartitionId { get; init; }
    public long? StreamSizeBytes { get; init; }
    public long? FileSizeBytes { get; init; }
}
