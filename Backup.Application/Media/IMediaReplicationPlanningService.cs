using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public interface IMediaReplicationPlanningService
{
    IReadOnlyList<MediaReplicationCopyAction> SelectCopyActions(
        IEnumerable<MediaReplicationPathObservation> observations
    );

    IReadOnlyList<MediaDownload> RemoveCopied(
        IEnumerable<MediaDownload> downloads,
        IEnumerable<MediaReplicationCopyAction> copied
    );
}
