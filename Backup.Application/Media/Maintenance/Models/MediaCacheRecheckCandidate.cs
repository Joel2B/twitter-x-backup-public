namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaCacheRecheckCandidate
{
    public required string Path { get; init; }
    public long? StreamSizeBytes { get; init; }
    public long? FileSizeBytes { get; init; }
}
