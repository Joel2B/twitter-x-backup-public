namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupStorageConsistencyDecision
{
    public required int MissingCount { get; init; }

    public required IReadOnlyList<string> ExtraPaths { get; init; }

    public required bool IsConsistent { get; init; }

    public required bool ShouldFail { get; init; }

    public required bool ShouldRemoveExtras { get; init; }
}
