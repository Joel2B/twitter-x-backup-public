using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkReportService : IMediaBackupChunkReportService
{
    public IReadOnlyList<MediaBackupChunkReportRow> Build(
        IEnumerable<MediaBackupChunkReportObservation> observations
    ) =>
        observations
            .Select(item => new MediaBackupChunkReportRow
            {
                ChunkId = item.ChunkId,
                PathCount = item.PathCount,
                SizeGiB = Math.Round(
                    item.SizeBytes / 1024m / 1024m / 1024m,
                    2,
                    MidpointRounding.ToZero
                ),
            })
            .ToList();
}
