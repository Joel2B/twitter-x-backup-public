namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkReportObservationInput
{
    public required int ChunkId { get; init; }

    public required int PathCount { get; init; }

    public required long SizeBytes { get; init; }
}
