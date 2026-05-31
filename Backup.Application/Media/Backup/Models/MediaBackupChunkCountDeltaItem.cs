namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkCountDeltaItem
{
    public required int ChunkId { get; init; }
    public required int BeforeCount { get; init; }
    public required int AfterCount { get; init; }
    public required int Difference { get; init; }
}
