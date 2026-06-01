namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkEntryRecord
{
    public required string Path { get; init; }
    public string? Hash { get; init; }
    public long? FileSize { get; init; }
    public uint? Crc32 { get; init; }
}
