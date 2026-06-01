namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupCalculateExecutionInput
{
    public required int TotalPathCount { get; init; }

    public required int ChunkCount { get; init; }

    public required int BackupIncreaseCount { get; init; }

    public required int ConfigIncreaseCount { get; init; }

    public required IReadOnlyCollection<int> ExistingChunkIds { get; init; }

    public required IReadOnlyList<MediaBackupChunkStateInput> ChunkStateInputs { get; init; }

    public required IReadOnlyCollection<string> AssignedCachePaths { get; init; }

    public required IReadOnlyList<MediaBackupPathCacheObservationInput> CacheObservationInputs { get; init; }

    public required IReadOnlyList<MediaBackupChunkPathsState> BeforeChunkPaths { get; init; }

    public required IReadOnlyDictionary<string, long> SizeByPath { get; init; }

    public required long MaxPathSizeBytes { get; init; }
}
