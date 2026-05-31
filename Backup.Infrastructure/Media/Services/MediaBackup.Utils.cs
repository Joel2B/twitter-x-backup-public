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
        List<MediaBackupChunkReportEntryInput> reportEntries = [];

        foreach (var kvp in _chunks)
        {
            if (kvp.Value.Data.Count == 0)
                continue;

            foreach (ChunkData chunkData in _chunks[kvp.Key].Data)
            {
                MediaCacheEntry? cache = await MediaData.GetCache(chunkData.Path);

                reportEntries.Add(
                    new MediaBackupChunkReportEntryInput
                    {
                        ChunkId = kvp.Key,
                        PathCount = kvp.Value.Data.Count,
                        FileSizeBytes = cache?.Size?.File ?? 0,
                    }
                );
            }
        }

        IReadOnlyList<MediaBackupChunkReportObservationInput> observationInputs =
            _mediaBackupChunkReportObservationAggregationService.Aggregate(reportEntries);

        IReadOnlyList<MediaBackupChunkReportObservation> observations =
            _mediaBackupChunkRuntimeCompositionService.BuildChunkReportObservations(
                observationInputs
            );

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
