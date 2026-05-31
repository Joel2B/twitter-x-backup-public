using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupIntegrityChangeDetectionService
{
    IReadOnlyList<MediaBackupIntegrityChange> Detect(
        IEnumerable<MediaBackupIntegrityObservation> observations
    );
}
