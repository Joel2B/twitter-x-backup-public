using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.IO;
using Backup.Infrastructure.Models.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupMetadataPhase(
    IMediaBackupChunkMetadataRefreshExecutionService chunkMetadataRefreshExecutionService,
    IMediaBackupArchiveMetadataMapService archiveMetadataMapService,
    IMediaBackupChunkPersistenceIOService chunkPersistenceIoService
) : IMediaBackupMetadataPhase
{
    private readonly IMediaBackupChunkMetadataRefreshExecutionService _chunkMetadataRefreshExecutionService =
        chunkMetadataRefreshExecutionService;
    private readonly IMediaBackupArchiveMetadataMapService _archiveMetadataMapService =
        archiveMetadataMapService;
    private readonly IMediaBackupChunkPersistenceIOService _chunkPersistenceIoService =
        chunkPersistenceIoService;

    public async Task SetFileSizes(
        MediaBackupRuntime runtime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        runtime.Logger.LogInformation("setting file sizes");

        foreach (
            KeyValuePair<int, Backup.Infrastructure.Media.Models.Backup.Chunk> kvp in runtime
                .Context
                .Chunks
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<MediaBackupChunkEntryState> entryStates = runtime.BuildChunkEntryStates(
                kvp.Value.Data
            );
            bool isNull = _chunkMetadataRefreshExecutionService.RequiresRefresh(entryStates);

            if (!isNull)
                continue;

            runtime.Logger.LogInformation("processing chunk {chunk}", kvp.Key);
            Dictionary<string, ZipEntry>? entries = await runtime.ReadChunkEntries(
                kvp.Value,
                "set-file-sizes"
            );

            if (entries is null)
                continue;

            runtime.Logger.LogInfo("updating data");
            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByArchivePath =
                _archiveMetadataMapService.BuildByArchivePath(
                    entries.Select(item => new MediaBackupArchiveMetadataInput
                    {
                        ArchivePath = item.Key,
                        FileSize = item.Value.FileSize,
                        Crc32 = item.Value.Crc32,
                    })
                );

            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> archiveMetadataByPath =
                BuildPathMetadataByPath(
                    kvp.Value.Data.Select(item => item.Path),
                    metadataByArchivePath
                );

            MediaBackupChunkMetadataRefreshExecutionResult refreshResult =
                _chunkMetadataRefreshExecutionService.Refresh(entryStates, archiveMetadataByPath);

            runtime.ApplyChunkEntryStates(kvp.Value, refreshResult.Entries);

            runtime.Logger.LogInfo("saving chunk");
            await _chunkPersistenceIoService.SaveChunk(runtime.MediaBackupData, kvp.Value);

            runtime.Logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }
    }

    private static IReadOnlyDictionary<
        string,
        MediaBackupChunkDataMetadata
    > BuildPathMetadataByPath(
        IEnumerable<string> paths,
        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByArchivePath
    ) =>
        paths.ToDictionary(
            path => path,
            path =>
            {
                string archivePath = MediaBackupPathProjection.ToArchivePath(path);

                if (
                    !metadataByArchivePath.TryGetValue(
                        archivePath,
                        out MediaBackupChunkDataMetadata? metadata
                    )
                )
                    return new MediaBackupChunkDataMetadata();

                return new MediaBackupChunkDataMetadata
                {
                    FileSize = metadata.FileSize,
                    Crc32 = metadata.Crc32,
                };
            },
            StringComparer.Ordinal
        );
}
