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
            bool isNull = _mediaBackupChunkMetadataPolicyService.RequiresRefresh(
                kvp.Value.Data.Select(item => new MediaBackupChunkDataMetadata
                {
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
                entries = zip.GetEntries().ToDictionary(o => o.FullName);
            }
            finally
            {
                _logger.LogInfo("disposing");
                zip.Dispose();
            }

            _logger.LogInfo("updating data");
            List<MediaBackupChunkMetadataRefreshCandidate> candidates = [];

            foreach (ChunkData item in kvp.Value.Data)
            {
                entries.TryGetValue(
                    _mediaBackupPathProjectionService.ToArchivePath(item.Path),
                    out ZipEntry? value
                );

                candidates.Add(
                    new MediaBackupChunkMetadataRefreshCandidate
                    {
                        Path = item.Path,
                        HasEntry = value is not null,
                        Current = new MediaBackupChunkDataMetadata
                        {
                            FileSize = item.FileSize,
                            Crc32 = item.Crc32,
                        },
                        Entry = new MediaBackupChunkDataMetadata
                        {
                            FileSize = value?.FileSize,
                            Crc32 = value?.Crc32,
                        },
                    }
                );
            }

            MediaBackupChunkMetadataRefreshPlan plan =
                _mediaBackupChunkMetadataRefreshPlanningService.Plan(candidates);
            Dictionary<string, MediaBackupChunkDataMetadata> updates = plan
                .Updates.ToDictionary(update => update.Path, update => update.Metadata);

            foreach (ChunkData item in kvp.Value.Data)
            {
                if (!updates.TryGetValue(item.Path, out MediaBackupChunkDataMetadata? metadata))
                    continue;

                item.FileSize = metadata.FileSize;
                item.Crc32 = metadata.Crc32;
            }

            _logger.LogInfo("saving chunk");
            await _mediaBackupData.Save([kvp.Value]);

            _logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }
    }
}
