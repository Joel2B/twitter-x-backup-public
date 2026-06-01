namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkAssignmentApplyResult
{
    public required IReadOnlyDictionary<
        int,
        IReadOnlyList<string>
    > AddedCachePathsByChunk { get; init; }

    public required IReadOnlyList<string> AddedOriginalPaths { get; init; }
}
