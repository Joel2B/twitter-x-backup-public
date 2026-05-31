namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkStateInput
{
    public required int Id { get; init; }

    public required int PathCount { get; init; }

    public required long SizeBytes { get; init; }
}
