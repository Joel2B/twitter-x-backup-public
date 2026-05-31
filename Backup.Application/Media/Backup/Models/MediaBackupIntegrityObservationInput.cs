namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupIntegrityObservationInput
{
    public required int ChunkId { get; init; }

    public required string Path { get; init; }

    public long? ExpectedFileSize { get; init; }

    public long? ActualFileSize { get; init; }

    public uint? ExpectedCrc32 { get; init; }

    public uint? ActualCrc32 { get; init; }
}
