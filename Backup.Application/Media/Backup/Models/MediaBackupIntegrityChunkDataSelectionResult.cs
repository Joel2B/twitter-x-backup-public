namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupIntegrityChunkDataSelectionResult
{
    public IReadOnlyList<string> SelectedPaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingPaths { get; init; } = Array.Empty<string>();
    public bool IsComplete => MissingPaths.Count == 0;
}
