namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupIntegrityChunkApplyResult
{
    public required IReadOnlyList<MediaBackupChunkEntryState> Entries { get; init; }

    public required IReadOnlyList<string> UpdatedPaths { get; init; }
}
