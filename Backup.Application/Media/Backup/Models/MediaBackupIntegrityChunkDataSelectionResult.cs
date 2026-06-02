namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupIntegrityChunkDataSelectionResult
{
    public IReadOnlyList<string> SelectedPaths { get; init; } = [];
    public IReadOnlyList<string> MissingPaths { get; init; } = [];
    public bool IsComplete => MissingPaths.Count == 0;
}
