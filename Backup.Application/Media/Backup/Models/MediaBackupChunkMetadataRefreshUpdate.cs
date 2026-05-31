namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkMetadataRefreshUpdate
{
    public string Path { get; init; } = string.Empty;
    public MediaBackupChunkDataMetadata Metadata { get; init; } = new();
}
