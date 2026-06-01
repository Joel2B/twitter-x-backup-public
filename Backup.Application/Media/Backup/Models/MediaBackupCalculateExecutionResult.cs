namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupCalculateExecutionResult
{
    public required MediaBackupChunkPlanningResult Planning { get; init; }

    public required MediaBackupChunkAssignmentResult Assignment { get; init; }

    public required MediaBackupChunkAssignmentApplyResult ApplyAssignments { get; init; }

    public required IReadOnlyList<MediaBackupChunkPathsState> AfterChunkPaths { get; init; }

    public required MediaBackupChunkDeltaLogPlan DeltaLogPlan { get; init; }
}
