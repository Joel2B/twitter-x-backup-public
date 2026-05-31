namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkMetadataRefreshPlan
{
    public IReadOnlyList<MediaBackupChunkMetadataRefreshUpdate> Updates { get; init; } =
        Array.Empty<MediaBackupChunkMetadataRefreshUpdate>();
}
