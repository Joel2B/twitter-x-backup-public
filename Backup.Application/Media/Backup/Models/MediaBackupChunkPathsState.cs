namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkPathsState
{
    public required int Id { get; init; }

    public required IReadOnlyList<string> Paths { get; init; }
}
