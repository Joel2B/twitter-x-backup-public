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
            bool isNull = _mediaBackupChunkMetadataOrchestrationService.RequiresRefresh(
                kvp.Value.Data.Select(item => new MediaBackupChunkPathMetadataState
                {
                    Path = item.Path,
                    FileSize = item.FileSize,
                    Crc32 = item.Crc32,
                })
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
            List<MediaBackupChunkMetadataObservationInput> observationInputs = [];

            foreach (ChunkData item in kvp.Value.Data)
            {
                entries.TryGetValue(
                    _mediaBackupPathProjectionService.ToArchivePath(item.Path),
                    out ZipEntry? value
                );

                observationInputs.Add(
                    new MediaBackupChunkMetadataObservationInput
                    {
                        Path = item.Path,
                        HasEntry = value is not null,
                        CurrentFileSize = item.FileSize,
                        CurrentCrc32 = item.Crc32,
                        EntryFileSize = value?.FileSize,
                        EntryCrc32 = value?.Crc32,
                    }
                );
            }

            IReadOnlyList<MediaBackupChunkMetadataObservation> observations =
                _mediaBackupChunkMetadataObservationCompositionService.BuildObservations(
                    observationInputs
                );

            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> updates =
                _mediaBackupChunkMetadataOrchestrationService.PlanUpdates(observations);

            foreach (ChunkData item in kvp.Value.Data)
            {
                if (!updates.TryGetValue(item.Path, out MediaBackupChunkDataMetadata? metadata))
                    continue;

                item.FileSize = metadata.FileSize;
                item.Crc32 = metadata.Crc32;
            }

            _logger.LogInfo("saving chunk");
            await _mediaBackupChunkPersistenceIoService.SaveChunk(_mediaBackupData, kvp.Value);

            _logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }
    }
}
