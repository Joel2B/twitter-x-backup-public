using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkReportService
{
    IReadOnlyList<MediaBackupChunkReportRow> Build(
        IEnumerable<MediaBackupChunkReportObservation> observations
    );
}
