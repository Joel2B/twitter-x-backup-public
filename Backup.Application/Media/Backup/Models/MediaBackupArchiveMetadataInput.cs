namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupArchiveMetadataInput
{
    public required string ArchivePath { get; init; }

    public long? FileSize { get; init; }

    public uint? Crc32 { get; init; }
}
