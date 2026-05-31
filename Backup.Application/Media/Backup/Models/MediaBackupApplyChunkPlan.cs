namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupApplyChunkPlan
{
    public required bool ShouldProcessChunk { get; init; }
    public required IReadOnlyList<MediaBackupApplyEntryCandidate> EntriesToAdd { get; init; }
    public required MediaBackupApplyFinalizePlan? FinalizePlan { get; init; }
}
