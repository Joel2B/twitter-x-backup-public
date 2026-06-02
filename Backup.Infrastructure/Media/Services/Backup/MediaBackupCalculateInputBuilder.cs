using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupCalculateInputBuilder(
    IMediaBackupChunkRuntimeCompositionService chunkRuntimeCompositionService
)
{
    private readonly IMediaBackupChunkRuntimeCompositionService _chunkRuntimeCompositionService =
        chunkRuntimeCompositionService;

    public async Task<MediaBackupCalculateExecutionInput> Build(
        MediaBackupRuntime runtime,
        CancellationToken cancellationToken = default
    )
    {
        Dictionary<int, Chunk> chunksClone = runtime.Context.Chunks.ToDictionary(
            item => item.Key,
            item => item.Value.Clone()
        );
        List<MediaBackupPathCacheObservationInput> cacheObservationInputs = [];

        foreach (string path in runtime.Context.Paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MediaCacheEntry? cache = await runtime.MediaData.GetCache(path);

            cacheObservationInputs.Add(
                new MediaBackupPathCacheObservationInput
                {
                    OriginalPath = path,
                    CacheExists = cache is not null,
                    CachePath = cache?.Path,
                    FileSizeBytes = cache?.Size?.File,
                }
            );
        }

        IReadOnlyList<MediaBackupChunkPathsState> beforeChunkPaths =
            _chunkRuntimeCompositionService.BuildChunkPathStates(
                chunksClone.Values.Select(chunk => new MediaBackupChunkPathsInput
                {
                    Id = chunk.Id,
                    Paths = chunk.Data.Select(data => data.Path).ToList(),
                })
            );
        IReadOnlyList<MediaBackupChunkStateInput> chunkStateInputs = runtime
            .Context.Chunks.Values.Select(chunk => new MediaBackupChunkStateInput
            {
                Id = chunk.Id,
                PathCount = chunk.Data.Count,
                SizeBytes = chunk.Data.Sum(item => item.FileSize ?? 0),
            })
            .ToList();
        HashSet<string> assignedCachePaths =
        [
            .. runtime.Context.Chunks.Values.SelectMany(chunk => chunk.Data).Select(item => item.Path),
        ];

        IReadOnlyDictionary<string, long> sizeByPath = cacheObservationInputs
            .Where(input => !string.IsNullOrWhiteSpace(input.CachePath))
            .GroupBy(input => input.CachePath!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                    group.FirstOrDefault(entry => entry.FileSizeBytes.HasValue)?.FileSizeBytes ?? 0,
                StringComparer.Ordinal
            );

        return new MediaBackupCalculateExecutionInput
        {
            TotalPathCount = runtime.Context.Paths.Count,
            ChunkCount = runtime.Context.Backup.Chunks.Total,
            BackupIncreaseCount = runtime.Context.Backup.Chunks.Path.Increase,
            ConfigIncreaseCount = runtime.Config.Chunk.Path.Increase,
            ExistingChunkIds = runtime.Context.Chunks.Keys.ToList(),
            ChunkStateInputs = chunkStateInputs,
            AssignedCachePaths = assignedCachePaths.ToList(),
            CacheObservationInputs = cacheObservationInputs,
            BeforeChunkPaths = beforeChunkPaths,
            SizeByPath = sizeByPath,
            MaxPathSizeBytes = runtime.Config.Chunk.Path.Size,
        };
    }
}
