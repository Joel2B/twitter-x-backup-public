using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupIntegrityPhase : IMediaBackupIntegrityPhase
{
    public async Task CheckIntegrity(MediaBackupRuntime runtime)
    {
        runtime.Logger.LogInformation("checking integrity backup");

        runtime.Context.Changes.Clear();
        List<MediaBackupIntegrityObservation> observations = [];

        foreach (
            KeyValuePair<int, Backup.Infrastructure.Media.Models.Backup.Chunk> kvp in runtime
                .Context
                .Chunks
        )
        {
            if (kvp.Value.Data.Count == 0)
                continue;

            runtime.Logger.LogInformation("processing chunk {chunk}", kvp.Key);
            IZipWriter? zip = await runtime.OpenChunkZipRead(
                runtime.Context.Chunks[kvp.Key],
                "check-integrity"
            );

            if (zip is null)
                continue;

            Dictionary<string, ZipEntry> entries;

            try
            {
                runtime.Logger.LogInfo("read zip");
                runtime.Logger.LogInfo("reading entries");
                entries = runtime.Dependencies.ZipEntryReaderIoService.ReadEntriesByFullName(zip);
            }
            finally
            {
                runtime.Logger.LogInfo("disposing");
                zip.Dispose();
            }

            runtime.Logger.LogInfo("checking changes");
            IReadOnlyList<MediaBackupChunkEntryState> entryStates = runtime.BuildChunkEntryStates(
                kvp.Value.Data
            );
            Dictionary<string, long?> actualFileSizeByPath = new(StringComparer.Ordinal);
            Dictionary<string, uint?> actualCrc32ByPath = new(StringComparer.Ordinal);

            foreach (Backup.Infrastructure.Media.Models.Backup.ChunkData item in kvp.Value.Data)
            {
                MediaCacheEntry? cache = await runtime.MediaData.GetCache(item.Path);
                entries.TryGetValue(
                    runtime.Dependencies.PathProjectionService.ToArchivePath(item.Path),
                    out ZipEntry? value2
                );

                actualFileSizeByPath[item.Path] = cache?.Size?.File;
                actualCrc32ByPath[item.Path] = value2?.Crc32;
            }

            observations.AddRange(
                runtime.Dependencies.IntegrityObservationCompositionService.BuildChunkObservations(
                    kvp.Key,
                    entryStates,
                    actualFileSizeByPath,
                    actualCrc32ByPath
                )
            );

            runtime.Logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }

        runtime.Context.Changes.AddRange(
            runtime.Dependencies.IntegrityChangeDetectionService.Detect(observations)
        );

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

    public async Task FixIntegrity(MediaBackupRuntime runtime)
    {
        IReadOnlyList<MediaBackupIntegrityChunkGroup> changes =
            runtime.Dependencies.IntegrityPlanningService.GroupByChunk(
                runtime.Dependencies.IntegrityObservationCompositionService.BuildPathChanges(
                    runtime.Context.Changes
                )
            );

        runtime.Logger.LogInformation("processing changes");

        foreach (MediaBackupIntegrityChunkGroup change in changes)
        {
            runtime.Logger.LogInformation("processing chunk {chunk}", change.ChunkId);
            IZipWriter? zip = await runtime.OpenChunkZipWrite(
                runtime.Context.Chunks[change.ChunkId],
                "fix-integrity"
            );

            if (zip is null)
                continue;

            try
            {
                runtime.Logger.LogInfo("applying fixes");

                foreach (string path in change.Paths)
                {
                    string relativePath = runtime.Dependencies.PathProjectionService.ToArchivePath(
                        path
                    );
                    await runtime.Dependencies.ZipMutationIoService.ReplaceEntryFromMediaStorage(
                        runtime.MediaData,
                        zip,
                        path,
                        relativePath
                    );

                    runtime.Logger.LogInfo("{path} processed", relativePath);
                }
            }
            finally
            {
                runtime.Logger.LogInfo("disposing");
                zip.Dispose();
            }
        }

        runtime.Logger.LogInformation("set new file sizes");

        foreach (MediaBackupIntegrityChunkGroup change in changes)
        {
            runtime.Logger.LogInformation("processing chunk {chunk}", change.ChunkId);

            IZipWriter? zip = await runtime.OpenChunkZipRead(
                runtime.Context.Chunks[change.ChunkId],
                "set-new-file-sizes-after-fix"
            );

            if (zip is null)
                continue;

            Dictionary<string, ZipEntry> entries;

            try
            {
                runtime.Logger.LogInfo("reading entries");
                entries = runtime.Dependencies.ZipEntryReaderIoService.ReadEntriesByFullName(zip);
            }
            finally
            {
                runtime.Logger.LogInfo("disposing");
                zip.Dispose();
            }

            runtime.Logger.LogInfo("expanding chunk");
            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByArchivePath =
                runtime.Dependencies.ArchiveMetadataMapService.BuildByArchivePath(
                    entries.Select(item => new MediaBackupArchiveMetadataInput
                    {
                        ArchivePath = item.Key,
                        FileSize = item.Value.FileSize,
                        Crc32 = item.Value.Crc32,
                    })
                );

            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByPath =
                runtime.Dependencies.PathArchiveMetadataProjectionService.BuildPathMetadataByPath(
                    change.Paths,
                    metadataByArchivePath
                );

            MediaBackupIntegrityChunkApplyResult applyResult =
                runtime.Dependencies.IntegrityChunkRefreshService.Refresh(
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
            await runtime.Dependencies.ChunkPersistenceIoService.SaveChunk(
                runtime.MediaBackupData,
                runtime.Context.Chunks[change.ChunkId]
            );
        }
    }
}
