using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupApplyChunkCoordinator(
    IMediaBackupChunkHashPreparationService chunkHashPreparationService,
    IMediaBackupChunkEntryStateService chunkEntryStateService,
    IMediaBackupApplyChunkPlanningService applyChunkPlanningService
)
{
    private readonly IMediaBackupChunkHashPreparationService _chunkHashPreparationService =
        chunkHashPreparationService;
    private readonly IMediaBackupChunkEntryStateService _chunkEntryStateService =
        chunkEntryStateService;
    private readonly IMediaBackupApplyChunkPlanningService _applyChunkPlanningService =
        applyChunkPlanningService;

    public async Task<bool> Execute(
        MediaBackupRuntime runtime,
        int chunkId,
        Chunk chunk,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<MediaBackupChunkEntryState> initialEntryStates =
                runtime.BuildChunkEntryStates(chunk.Data);
            IReadOnlyList<string> pathsNeedingHash =
                _chunkHashPreparationService.SelectPathsNeedingHash(initialEntryStates);
            Dictionary<string, string?> hashByPath = new(StringComparer.Ordinal);

            foreach (string path in pathsNeedingHash)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? hash = await runtime.MediaData.GetHash(UtilsPath.NormalizePath(path));
                hashByPath[path] = hash;

                if (hash is null)
                    runtime.Logger.LogInfo("error in hash: {path}", path);
            }

            IReadOnlyList<MediaBackupChunkEntryState> hashedEntryStates =
                _chunkHashPreparationService.ApplyHashes(initialEntryStates, hashByPath);
            runtime.ApplyChunkEntryStates(chunk, hashedEntryStates);

            if (!hashByPath.Values.Any(hash => hash is not null))
                return true;

            IReadOnlyList<MediaBackupApplyChunkPathState> chunkPaths =
                _chunkEntryStateService.BuildApplyChunkPathStates(hashedEntryStates);

            if (!chunkPaths.Any(item => item.HasHash))
                return true;

            runtime.Logger.LogInformation("processing chunk {chunk}", chunkId);

            MediaBackupApplyChunkPlan? chunkPlan = null;
            HashSet<string>? storagePaths = null;

            bool mutated = await runtime.MutateChunkZip(
                chunk,
                "apply",
                async zip =>
                {
                    runtime.Logger.LogInfo("reading entries");
                    storagePaths = [.. zip.GetEntries().Select(o => o.FullName)];

                    chunkPlan = _applyChunkPlanningService.Plan(
                        chunkPaths,
                        storagePaths,
                        runtime.Context.Backup.Chunks.Ids,
                        chunkId
                    );

                    if (!chunkPlan.ShouldProcessChunk || chunkPlan.FinalizePlan is null)
                        return;

                    foreach (MediaBackupApplyEntryCandidate item in chunkPlan.EntriesToAdd)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        storagePaths.Add(item.ArchivePath);

                        await using Stream read = await runtime.MediaData.Read(
                            UtilsPath.NormalizePath(item.SourcePath)
                        );
                        await zip.AddEntry(item.ArchivePath, read);
                    }

                    MediaBackupApplyFinalizePlan finalizePlan = chunkPlan.FinalizePlan;
                    MediaBackupStorageConsistencyDecision decision =
                        finalizePlan.ConsistencyDecision;

                    runtime.Logger.LogInformation(
                        "{memory}/{storage}:{missing}/{extras}",
                        chunk.Data.Count,
                        storagePaths.Count,
                        decision.MissingCount,
                        decision.ExtraPaths.Count
                    );

                    if (decision.ShouldFail)
                    {
                        throw new InvalidOperationException(
                            $"backup apply consistency check failed for chunk {chunkId}"
                        );
                    }

                    if (!decision.ShouldRemoveExtras)
                        return;

                    foreach (string path in decision.ExtraPaths)
                        zip.RemoveEntry(path);

                    runtime.Logger.LogInformation(
                        "{extras} paths removed in storage",
                        decision.ExtraPaths.Count
                    );
                }
            );

            if (
                !mutated
                || chunkPlan is null
                || !chunkPlan.ShouldProcessChunk
                || chunkPlan.FinalizePlan is null
            )
            {
                return true;
            }

            runtime.Context.Backup.Chunks.Ids = chunkPlan.FinalizePlan.ChunkIds.ToList();

            await runtime.MediaBackupData.SaveBackup(runtime.Context.Backup);
            await runtime.MediaBackupData.Save([chunk]);

            runtime.Logger.LogInformation("chunk {chunk} processed", chunkId);
            return true;
        }
        catch (Exception ex)
        {
            runtime.Logger.LogError(ex, "error applying backup chunk {chunk}", chunkId);
            await runtime.RecoverApplyFailure(chunk);
            return false;
        }
    }
}
