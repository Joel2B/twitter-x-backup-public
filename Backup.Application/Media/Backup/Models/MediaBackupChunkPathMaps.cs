namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupChunkPathMaps
{
    public required IReadOnlyDictionary<int, IReadOnlyList<string>> BeforePathsByChunk { get; init; }

    public required IReadOnlyDictionary<int, IReadOnlyList<string>> AfterPathsByChunk { get; init; }

    public required IReadOnlyList<string> DistinctPathsForSizeLookup { get; init; }
}
