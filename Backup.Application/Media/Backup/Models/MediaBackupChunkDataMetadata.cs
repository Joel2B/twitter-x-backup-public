namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkDataMetadata
{
    public long? FileSize { get; init; }

    public uint? Crc32 { get; init; }
}
