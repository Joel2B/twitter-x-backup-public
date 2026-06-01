namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkLoadDecision
{
    public required MediaBackupChunkLoadAction Action { get; init; }
    public required IReadOnlyList<MediaBackupChunkReadDescriptor> ReadDescriptors { get; init; }
}
