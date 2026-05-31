namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkMetadataRefreshExecutionResult
{
    public required IReadOnlyList<MediaBackupChunkEntryState> Entries { get; init; }

    public required int UpdatedCount { get; init; }
}
