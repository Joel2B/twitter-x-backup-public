namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkReconciliationResult
{
    public required int MissingCount { get; init; }

    public required IReadOnlyList<string> ExtraPaths { get; init; }

    public bool IsConsistent => MissingCount == 0 && ExtraPaths.Count == 0;

    public bool ShouldFail => MissingCount != 0;
}
