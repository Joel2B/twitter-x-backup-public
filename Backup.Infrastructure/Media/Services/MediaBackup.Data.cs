using Backup.Infrastructure.Logging;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Models.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public partial class MediaBackup
{
    private async Task SetFileSizes()
    {
        _logger.LogInformation("setting file sizes");

        foreach (var kvp in _chunks)
        {
            IReadOnlyList<MediaBackupChunkEntryState> entryStates = BuildChunkEntryStates(
                kvp.Value.Data
            );
            bool isNull = _mediaBackupChunkMetadataRefreshExecutionService.RequiresRefresh(
                entryStates
            );

            if (!isNull)
                continue;

            _logger.LogInformation("processing chunk {chunk}", kvp.Key);
            IZipWriter? zip = await OpenChunkZipRead(kvp.Value, "set-file-sizes");

            if (zip is null)
                continue;

            Dictionary<string, ZipEntry> entries;

            try
            {
                _logger.LogInfo("read zip");
                _logger.LogInfo("reading entries");
                entries = _mediaBackupZipEntryReaderIoService.ReadEntriesByFullName(zip);
            }
            finally
            {
                _logger.LogInfo("disposing");
                zip.Dispose();
            }

            _logger.LogInfo("updating data");
            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByArchivePath =
                _mediaBackupArchiveMetadataMapService.BuildByArchivePath(
                    entries.Select(item => new MediaBackupArchiveMetadataInput
                    {
                        ArchivePath = item.Key,
                        FileSize = item.Value.FileSize,
                        Crc32 = item.Value.Crc32,
                    })
                );

            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> archiveMetadataByPath =
                _mediaBackupPathArchiveMetadataProjectionService.BuildPathMetadataByPath(
                    kvp.Value.Data.Select(item => item.Path),
                    metadataByArchivePath
                );

            MediaBackupChunkMetadataRefreshExecutionResult refreshResult =
                _mediaBackupChunkMetadataRefreshExecutionService.Refresh(
                    entryStates,
                    archiveMetadataByPath
                );

            ApplyChunkEntryStates(kvp.Value, refreshResult.Entries);

            _logger.LogInfo("saving chunk");
            await _mediaBackupChunkPersistenceIoService.SaveChunk(_mediaBackupData, kvp.Value);

            _logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }
    }
}
