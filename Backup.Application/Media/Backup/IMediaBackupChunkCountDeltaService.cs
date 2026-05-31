using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkCountDeltaService
{
    MediaBackupChunkCountDeltaResult Compare(
        IEnumerable<MediaBackupChunkCountState> before,
        IEnumerable<MediaBackupChunkCountState> after
    );
}
