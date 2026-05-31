namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkCountDeltaResult
{
    public required int TotalAddedPaths { get; init; }
    public required IReadOnlyList<MediaBackupChunkCountDeltaItem> Items { get; init; }
}
