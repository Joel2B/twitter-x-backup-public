namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkReportRow
{
    public required int ChunkId { get; init; }

    public required int PathCount { get; init; }

    public required decimal SizeGiB { get; init; }
}
