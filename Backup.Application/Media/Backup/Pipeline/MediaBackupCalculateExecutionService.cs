using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupCalculateExecutionService(
    IMediaBackupChunkPlanningService mediaBackupChunkPlanningService,
    IMediaBackupChunkRuntimeCompositionService mediaBackupChunkRuntimeCompositionService,
    IMediaBackupPathCandidateCompositionService mediaBackupPathCandidateCompositionService,
    IMediaBackupChunkAssignmentService mediaBackupChunkAssignmentService,
    IMediaBackupChunkAssignmentApplyService mediaBackupChunkAssignmentApplyService,
    IMediaBackupChunkSnapshotCompositionService mediaBackupChunkSnapshotCompositionService,
    IMediaBackupChunkCountDeltaService mediaBackupChunkCountDeltaService,
    IMediaBackupChunkDeltaInputCompositionService mediaBackupChunkDeltaInputCompositionService,
    IMediaBackupChunkDeltaLogPlanningService mediaBackupChunkDeltaLogPlanningService
) : IMediaBackupCalculateExecutionService
{
    private readonly IMediaBackupChunkPlanningService _mediaBackupChunkPlanningService =
        mediaBackupChunkPlanningService;
    private readonly IMediaBackupChunkRuntimeCompositionService _mediaBackupChunkRuntimeCompositionService =
        mediaBackupChunkRuntimeCompositionService;
    private readonly IMediaBackupPathCandidateCompositionService _mediaBackupPathCandidateCompositionService =
        mediaBackupPathCandidateCompositionService;
    private readonly IMediaBackupChunkAssignmentService _mediaBackupChunkAssignmentService =
        mediaBackupChunkAssignmentService;
    private readonly IMediaBackupChunkAssignmentApplyService _mediaBackupChunkAssignmentApplyService =
        mediaBackupChunkAssignmentApplyService;
    private readonly IMediaBackupChunkSnapshotCompositionService _mediaBackupChunkSnapshotCompositionService =
        mediaBackupChunkSnapshotCompositionService;
    private readonly IMediaBackupChunkCountDeltaService _mediaBackupChunkCountDeltaService =
        mediaBackupChunkCountDeltaService;
    private readonly IMediaBackupChunkDeltaInputCompositionService _mediaBackupChunkDeltaInputCompositionService =
        mediaBackupChunkDeltaInputCompositionService;
    private readonly IMediaBackupChunkDeltaLogPlanningService _mediaBackupChunkDeltaLogPlanningService =
        mediaBackupChunkDeltaLogPlanningService;

    public MediaBackupCalculateExecutionResult Execute(MediaBackupCalculateExecutionInput input)
    {
        MediaBackupChunkPlanningResult plan = _mediaBackupChunkPlanningService.Plan(
            input.TotalPathCount,
            input.ChunkCount,
            input.BackupIncreaseCount,
            input.ConfigIncreaseCount,
            input.ExistingChunkIds
        );

        IReadOnlyList<MediaBackupChunkState> chunkStates =
            _mediaBackupChunkRuntimeCompositionService.BuildChunkStates(input.ChunkStateInputs);
        IReadOnlyList<MediaBackupPathCacheObservation> candidateObservations =
            BuildPathCacheObservations(input.CacheObservationInputs);
        IReadOnlyList<MediaBackupPathCandidate> candidates =
            _mediaBackupPathCandidateCompositionService.Compose(
                candidateObservations,
                input.AssignedCachePaths.ToHashSet(StringComparer.Ordinal)
            );

        MediaBackupChunkAssignmentResult assignment = _mediaBackupChunkAssignmentService.Assign(
            chunkStates,
            candidates,
            input.ChunkCount,
            plan.PathsPerChunk,
            plan.IncreaseCount,
            input.MaxPathSizeBytes
        );

        MediaBackupChunkAssignmentApplyResult applyAssignments =
            _mediaBackupChunkAssignmentApplyService.Apply(assignment.Assignments);

        IReadOnlyList<MediaBackupChunkPathsState> afterChunkPaths = BuildAfterChunkPaths(
            input.BeforeChunkPaths,
            applyAssignments.AddedCachePathsByChunk
        );
        IReadOnlyList<MediaBackupChunkCountState> beforeCountStates =
            _mediaBackupChunkSnapshotCompositionService.BuildChunkCountStates(
                input.BeforeChunkPaths
            );
        IReadOnlyList<MediaBackupChunkCountState> afterCountStates =
            _mediaBackupChunkSnapshotCompositionService.BuildChunkCountStates(afterChunkPaths);

        MediaBackupChunkCountDeltaResult deltas = _mediaBackupChunkCountDeltaService.Compare(
            beforeCountStates,
            afterCountStates
        );
        MediaBackupChunkPathMaps pathMaps =
            _mediaBackupChunkSnapshotCompositionService.BuildPathMaps(
                input.BeforeChunkPaths,
                afterChunkPaths
            );

        IReadOnlyList<MediaBackupChunkDeltaLogInput> deltaLogInputs =
            _mediaBackupChunkDeltaInputCompositionService.Compose(
                deltas.Items,
                pathMaps.BeforePathsByChunk,
                pathMaps.AfterPathsByChunk,
                input.SizeByPath
            );
        MediaBackupChunkDeltaLogPlan deltaLogPlan = _mediaBackupChunkDeltaLogPlanningService.Plan(
            deltaLogInputs,
            deltas.TotalAddedPaths,
            applyAssignments.AddedOriginalPaths.Count
        );

        return new MediaBackupCalculateExecutionResult
        {
            Planning = plan,
            Assignment = assignment,
            ApplyAssignments = applyAssignments,
            AfterChunkPaths = afterChunkPaths,
            DeltaLogPlan = deltaLogPlan,
        };
    }

    private static IReadOnlyList<MediaBackupChunkPathsState> BuildAfterChunkPaths(
        IReadOnlyList<MediaBackupChunkPathsState> before,
        IReadOnlyDictionary<int, IReadOnlyList<string>> addedByChunk
    )
    {
        Dictionary<int, List<string>> after = before.ToDictionary(
            item => item.Id,
            item => item.Paths.ToList()
        );

        foreach ((int chunkId, IReadOnlyList<string> addedPaths) in addedByChunk)
        {
            if (!after.TryGetValue(chunkId, out List<string>? current))
            {
                current = [];
                after[chunkId] = current;
            }

            current.AddRange(addedPaths);
        }

        return after
            .OrderBy(item => item.Key)
            .Select(item => new MediaBackupChunkPathsState { Id = item.Key, Paths = item.Value })
            .ToList();
    }

    private static IReadOnlyList<MediaBackupPathCacheObservation> BuildPathCacheObservations(
        IEnumerable<MediaBackupPathCacheObservationInput> inputs
    ) =>
        inputs
            .Select(input => new MediaBackupPathCacheObservation
            {
                OriginalPath = input.OriginalPath,
                CacheExists = input.CacheExists,
                CachePath = input.CachePath ?? string.Empty,
                FileSizeBytes = input.FileSizeBytes,
            })
            .ToList();
}
