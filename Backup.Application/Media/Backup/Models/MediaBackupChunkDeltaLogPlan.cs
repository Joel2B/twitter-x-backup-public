namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkDeltaLogPlan
{
    public required int TotalAddedPaths { get; init; }
    public required int AddedPathCount { get; init; }
    public required IReadOnlyList<MediaBackupChunkDeltaLogRow> Rows { get; init; }
}
