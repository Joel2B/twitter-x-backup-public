namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkMetadataRefreshCandidate
{
    public string Path { get; init; } = string.Empty;
    public bool HasEntry { get; init; }
    public MediaBackupChunkDataMetadata Current { get; init; } = new();
    public MediaBackupChunkDataMetadata Entry { get; init; } = new();
}
