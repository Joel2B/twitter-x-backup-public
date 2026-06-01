namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheRecheckProbeInput
{
    public required string Path { get; init; }
    public int? PartitionId { get; init; }
    public long? StreamSizeBytes { get; init; }
}
