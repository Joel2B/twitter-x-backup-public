using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupChunkReportCoordinator(
    IMediaBackupChunkReportObservationAggregationService chunkReportObservationAggregationService,
    IMediaBackupChunkRuntimeCompositionService chunkRuntimeCompositionService,
    IMediaBackupChunkReportService chunkReportService
)
{
    private readonly IMediaBackupChunkReportObservationAggregationService _chunkReportObservationAggregationService =
        chunkReportObservationAggregationService;
    private readonly IMediaBackupChunkRuntimeCompositionService _chunkRuntimeCompositionService =
        chunkRuntimeCompositionService;
    private readonly IMediaBackupChunkReportService _chunkReportService = chunkReportService;

    public async Task ShowInfoChunks(MediaBackupRuntime runtime, string? id)
    {
        runtime.Logger.LogInfo("{id,-3} {paths,-10} {size}", "id", "paths", "size (GiB)");
        List<MediaBackupChunkReportEntryInput> reportEntries = [];

        foreach (KeyValuePair<int, Chunk> kvp in runtime.Context.Chunks)
        {
            if (kvp.Value.Data.Count == 0)
                continue;

            foreach (ChunkData chunkData in runtime.Context.Chunks[kvp.Key].Data)
            {
                MediaCacheEntry? cache = await runtime.MediaData.GetCache(chunkData.Path);

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
            _chunkReportObservationAggregationService.Aggregate(reportEntries);

        IReadOnlyList<MediaBackupChunkReportObservation> observations =
            _chunkRuntimeCompositionService.BuildChunkReportObservations(observationInputs);

        IReadOnlyList<MediaBackupChunkReportRow> rows = _chunkReportService.Build(observations);

        foreach (MediaBackupChunkReportRow row in rows)
        {
            runtime.Logger.LogInfo(
                "{id,-3} {paths,-10} {size}",
                row.ChunkId,
                row.PathCount,
                row.SizeGiB
            );
        }
    }
}
