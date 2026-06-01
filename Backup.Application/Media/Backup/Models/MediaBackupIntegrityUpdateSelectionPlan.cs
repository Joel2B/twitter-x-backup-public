namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupIntegrityUpdateSelectionPlan
{
    public required IReadOnlyList<string> SelectedPaths { get; init; }
    public required IReadOnlyDictionary<
        string,
        MediaBackupChunkDataMetadata
    > PathMetadata { get; init; }
}
