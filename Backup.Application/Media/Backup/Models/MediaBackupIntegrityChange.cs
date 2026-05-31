namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupIntegrityChange
{
    public required int ChunkId { get; init; }

    public required string Path { get; init; }

    public long? ExpectedFileSize { get; init; }

    public long? ActualFileSize { get; init; }

    public long? ExpectedCrc32 { get; init; }

    public long? ActualCrc32 { get; init; }
}
