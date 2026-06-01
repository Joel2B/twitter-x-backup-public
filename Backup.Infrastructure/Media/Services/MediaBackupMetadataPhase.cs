using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Models.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupMetadataPhase : IMediaBackupMetadataPhase
{
    public async Task SetFileSizes(MediaBackupRuntime runtime)
    {
        runtime.Logger.LogInformation("setting file sizes");

        foreach (
            KeyValuePair<int, Backup.Infrastructure.Media.Models.Backup.Chunk> kvp in runtime
                .Context
                .Chunks
        )
        {
            IReadOnlyList<MediaBackupChunkEntryState> entryStates = runtime.BuildChunkEntryStates(
                kvp.Value.Data
            );
            bool isNull = runtime.Dependencies.ChunkMetadataRefreshExecutionService.RequiresRefresh(
                entryStates
            );

            if (!isNull)
                continue;

            runtime.Logger.LogInformation("processing chunk {chunk}", kvp.Key);
            IZipWriter? zip = await runtime.OpenChunkZipRead(kvp.Value, "set-file-sizes");

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

            runtime.Logger.LogInfo("updating data");
            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByArchivePath =
                runtime.Dependencies.ArchiveMetadataMapService.BuildByArchivePath(
                    entries.Select(item => new MediaBackupArchiveMetadataInput
                    {
                        ArchivePath = item.Key,
                        FileSize = item.Value.FileSize,
                        Crc32 = item.Value.Crc32,
                    })
                );

            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> archiveMetadataByPath =
                runtime.Dependencies.PathArchiveMetadataProjectionService.BuildPathMetadataByPath(
                    kvp.Value.Data.Select(item => item.Path),
                    metadataByArchivePath
                );

            MediaBackupChunkMetadataRefreshExecutionResult refreshResult =
                runtime.Dependencies.ChunkMetadataRefreshExecutionService.Refresh(
                    entryStates,
                    archiveMetadataByPath
                );

            runtime.ApplyChunkEntryStates(kvp.Value, refreshResult.Entries);

            runtime.Logger.LogInfo("saving chunk");
            await runtime.Dependencies.ChunkPersistenceIoService.SaveChunk(
                runtime.MediaBackupData,
                kvp.Value
            );

            runtime.Logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }
    }
}
