namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkReportEntryInput
{
    public required int ChunkId { get; init; }

    public required int PathCount { get; init; }

    public long FileSizeBytes { get; init; }
}
