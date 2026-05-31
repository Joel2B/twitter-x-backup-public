namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkPlanningResult
{
    public required int PathsPerChunk { get; init; }
    public required int IncreaseCount { get; init; }
    public required int CapacityPerChunk { get; init; }
    public required IReadOnlyList<int> MissingChunkIds { get; init; }
    public required bool RequiresSeedChunk { get; init; }
}
