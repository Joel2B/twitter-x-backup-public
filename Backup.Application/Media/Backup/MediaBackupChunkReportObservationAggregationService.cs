using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkReportObservationAggregationService
    : IMediaBackupChunkReportObservationAggregationService
{
    public IReadOnlyList<MediaBackupChunkReportObservationInput> Aggregate(
        IEnumerable<MediaBackupChunkReportEntryInput> entries
    ) =>
        entries
            .GroupBy(item => item.ChunkId)
            .Select(group => new MediaBackupChunkReportObservationInput
            {
                ChunkId = group.Key,
                PathCount = group.Select(item => item.PathCount).DefaultIfEmpty(0).Max(),
                SizeBytes = group.Sum(item => item.FileSizeBytes),
            })
            .OrderBy(item => item.ChunkId)
            .ToList();
}
