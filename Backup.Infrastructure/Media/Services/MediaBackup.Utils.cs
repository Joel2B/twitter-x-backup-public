using Backup.Infrastructure.Logging;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public partial class MediaBackup
{
    private async Task ShowInfoChunks()
    {
        _logger.LogInfo("{id,-3} {paths,-10} {size}", "id", "paths", "size (GiB)");
        List<MediaBackupChunkReportObservation> observations = [];

        foreach (var kvp in _chunks)
        {
            if (kvp.Value.Data.Count == 0)
                continue;

            long size = 0;

            foreach (ChunkData chunkData in _chunks[kvp.Key].Data)
            {
                MediaCacheEntry? cache = await MediaData.GetCache(chunkData.Path);

                if (cache is not null)
                    size += cache.Size?.File ?? 0;
            }

            observations.Add(
                new MediaBackupChunkReportObservation
                {
                    ChunkId = kvp.Key,
                    PathCount = kvp.Value.Data.Count,
                    SizeBytes = size,
                }
            );
        }

        IReadOnlyList<MediaBackupChunkReportRow> rows = _mediaBackupChunkReportService.Build(
            observations
        );

        foreach (MediaBackupChunkReportRow row in rows)
        {
            _logger.LogInformation(
                "{id,-3} {paths,-10} {size}",
                row.ChunkId,
                row.PathCount,
                row.SizeGiB
            );
        }
    }
}
