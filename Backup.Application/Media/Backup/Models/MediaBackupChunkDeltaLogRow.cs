namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkDeltaLogRow
{
    public required int ChunkId { get; init; }
    public required int BeforeCount { get; init; }
    public required int AfterCount { get; init; }
    public required int Difference { get; init; }
    public required decimal SizeBeforeGiB { get; init; }
    public required decimal SizeAfterGiB { get; init; }
}
