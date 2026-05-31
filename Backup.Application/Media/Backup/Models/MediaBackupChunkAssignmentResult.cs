namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkAssignmentResult
{
    public required int InitialChunkId { get; init; }

    public required IReadOnlyList<MediaBackupPathAssignment> Assignments { get; init; }
}
