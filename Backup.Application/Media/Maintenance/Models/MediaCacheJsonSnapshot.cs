namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheJsonSnapshot
{
    public required string Path { get; init; }
    public long? StreamSizeBytes { get; init; }
    public long? FileSizeBytes { get; init; }
    public int? PartitionId { get; init; }
}
