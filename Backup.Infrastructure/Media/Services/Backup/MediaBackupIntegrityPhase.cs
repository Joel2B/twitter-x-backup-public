using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.IO;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupIntegrityPhase(
    IMediaBackupPathProjectionService pathProjectionService,
    IMediaBackupIntegrityObservationCompositionService integrityObservationCompositionService,
    IMediaBackupIntegrityChangeDetectionService integrityChangeDetectionService,
    IMediaBackupIntegrityPlanningService integrityPlanningService,
    IMediaBackupZipMutationIOService zipMutationIoService,
    IMediaBackupArchiveMetadataMapService archiveMetadataMapService,
    IMediaBackupPathArchiveMetadataProjectionService pathArchiveMetadataProjectionService,
    IMediaBackupIntegrityChunkRefreshService integrityChunkRefreshService,
    IMediaBackupChunkPersistenceIOService chunkPersistenceIoService
) : IMediaBackupIntegrityPhase
{
    private readonly IMediaBackupPathProjectionService _pathProjectionService =
        pathProjectionService;
    private readonly IMediaBackupIntegrityObservationCompositionService _integrityObservationCompositionService =
        integrityObservationCompositionService;
    private readonly IMediaBackupIntegrityChangeDetectionService _integrityChangeDetectionService =
        integrityChangeDetectionService;
    private readonly IMediaBackupIntegrityPlanningService _integrityPlanningService =
        integrityPlanningService;
    private readonly IMediaBackupZipMutationIOService _zipMutationIoService = zipMutationIoService;
    private readonly IMediaBackupArchiveMetadataMapService _archiveMetadataMapService =
        archiveMetadataMapService;
    private readonly IMediaBackupPathArchiveMetadataProjectionService _pathArchiveMetadataProjectionService =
        pathArchiveMetadataProjectionService;
    private readonly IMediaBackupIntegrityChunkRefreshService _integrityChunkRefreshService =
        integrityChunkRefreshService;
    private readonly IMediaBackupChunkPersistenceIOService _chunkPersistenceIoService =
        chunkPersistenceIoService;

    public async Task CheckIntegrity(
        MediaBackupRuntime runtime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        runtime.Logger.LogInformation("checking integrity backup");

        runtime.Context.Changes.Clear();
        List<MediaBackupIntegrityObservation> observations = [];

        foreach (
            KeyValuePair<int, Backup.Infrastructure.Media.Models.Backup.Chunk> kvp in runtime
                .Context
                .Chunks
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (kvp.Value.Data.Count == 0)
                continue;

            runtime.Logger.LogInformation("processing chunk {chunk}", kvp.Key);
            Dictionary<string, ZipEntry>? entries = await runtime.ReadChunkEntries(
                runtime.Context.Chunks[kvp.Key],
                "check-integrity"
            );

            if (entries is null)
                continue;

            runtime.Logger.LogInfo("checking changes");
            IReadOnlyList<MediaBackupChunkEntryState> entryStates = runtime.BuildChunkEntryStates(
                kvp.Value.Data
            );
            Dictionary<string, long?> actualFileSizeByPath = new(StringComparer.Ordinal);
            Dictionary<string, uint?> actualCrc32ByPath = new(StringComparer.Ordinal);

            foreach (Backup.Infrastructure.Media.Models.Backup.ChunkData item in kvp.Value.Data)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MediaCacheEntry? cache = await runtime.MediaData.GetCache(item.Path);
                entries.TryGetValue(
                    _pathProjectionService.ToArchivePath(item.Path),
                    out ZipEntry? value2
                );

                actualFileSizeByPath[item.Path] = cache?.Size?.File;
                actualCrc32ByPath[item.Path] = value2?.Crc32;
            }

            observations.AddRange(
                _integrityObservationCompositionService.BuildChunkObservations(
                    kvp.Key,
                    entryStates,
                    actualFileSizeByPath,
                    actualCrc32ByPath
                )
            );

            runtime.Logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }

        runtime.Context.Changes.AddRange(_integrityChangeDetectionService.Detect(observations));

        if (runtime.Context.Changes.Count > 0)
            runtime.Logger.LogInfo(
                "{id,-3} {diff1,-10} {diff2,-10} {diff,-5} {path}",
                "id",
                "diff1",
                "diff2",
                "diff",
                "path"
            );

        foreach (MediaBackupIntegrityChange change in runtime.Context.Changes)
        {
            runtime.Logger.LogInformation(
                "{id,-3} {diff1,-10} {diff2,-10} {diff,-5} {path}",
                change.ChunkId,
                change.ExpectedFileSize,
                change.ActualFileSize,
                change.ExpectedFileSize - change.ActualFileSize,
                change.Path
            );
        }
    }

    public async Task FixIntegrity(
        MediaBackupRuntime runtime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MediaBackupIntegrityChunkGroup> changes =
            _integrityPlanningService.GroupByChunk(
                _integrityObservationCompositionService.BuildPathChanges(runtime.Context.Changes)
            );

        runtime.Logger.LogInformation("processing changes");

        foreach (MediaBackupIntegrityChunkGroup change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runtime.Logger.LogInformation("processing chunk {chunk}", change.ChunkId);
            bool mutated = await runtime.MutateChunkZip(
                runtime.Context.Chunks[change.ChunkId],
                "fix-integrity",
                async zip =>
                {
                    runtime.Logger.LogInfo("applying fixes");

                    foreach (string path in change.Paths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string relativePath = _pathProjectionService.ToArchivePath(path);
                        await _zipMutationIoService.ReplaceEntryFromMediaStorage(
                            runtime.MediaData,
                            zip,
                            path,
                            relativePath
                        );

                        runtime.Logger.LogInfo("{path} processed", relativePath);
                    }
                }
            );

            if (!mutated)
                continue;
        }

        runtime.Logger.LogInformation("set new file sizes");

        foreach (MediaBackupIntegrityChunkGroup change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runtime.Logger.LogInformation("processing chunk {chunk}", change.ChunkId);

            Dictionary<string, ZipEntry>? entries = await runtime.ReadChunkEntries(
                runtime.Context.Chunks[change.ChunkId],
                "set-new-file-sizes-after-fix"
            );

            if (entries is null)
                continue;

            runtime.Logger.LogInfo("expanding chunk");
            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByArchivePath =
                _archiveMetadataMapService.BuildByArchivePath(
                    entries.Select(item => new MediaBackupArchiveMetadataInput
                    {
                        ArchivePath = item.Key,
                        FileSize = item.Value.FileSize,
                        Crc32 = item.Value.Crc32,
                    })
                );

            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByPath =
                _pathArchiveMetadataProjectionService.BuildPathMetadataByPath(
                    change.Paths,
                    metadataByArchivePath
                );

            MediaBackupIntegrityChunkApplyResult applyResult =
                _integrityChunkRefreshService.Refresh(
                    change.Paths,
                    runtime.Context.Chunks[change.ChunkId].Data.Select(chunkData => chunkData.Path),
                    metadataByPath,
                    runtime.BuildChunkEntryStates(runtime.Context.Chunks[change.ChunkId].Data)
                );

            runtime.ApplyChunkEntryStates(
                runtime.Context.Chunks[change.ChunkId],
                applyResult.Entries
            );

            foreach (string path in applyResult.UpdatedPaths)
                runtime.Logger.LogInfo("{path} updated", path);

            runtime.Logger.LogInfo("saving chunk");
            await _chunkPersistenceIoService.SaveChunk(
                runtime.MediaBackupData,
                runtime.Context.Chunks[change.ChunkId]
            );
        }
    }
}
