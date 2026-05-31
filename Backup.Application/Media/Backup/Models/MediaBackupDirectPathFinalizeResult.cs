namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupDirectPathFinalizeResult
{
    public required IReadOnlyList<string> PathsInBoth { get; init; }
    public required IReadOnlyList<string> DirectPaths { get; init; }
}
