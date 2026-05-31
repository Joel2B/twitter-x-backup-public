namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkMetadataObservation
{
    public required string Path { get; init; }
    public required bool HasEntry { get; init; }
    public long? CurrentFileSize { get; init; }
    public uint? CurrentCrc32 { get; init; }
    public long? EntryFileSize { get; init; }
    public uint? EntryCrc32 { get; init; }
}
