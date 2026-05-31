namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupApplyFinalizePlan
{
    public required MediaBackupStorageConsistencyDecision ConsistencyDecision { get; init; }
    public required IReadOnlyList<int> ChunkIds { get; init; }
}
