namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkDeltaLogInput
{
    public required int ChunkId { get; init; }
    public required int BeforeCount { get; init; }
    public required int AfterCount { get; init; }
    public required int Difference { get; init; }
    public required long SizeBeforeBytes { get; init; }
    public required long SizeAfterBytes { get; init; }
}
