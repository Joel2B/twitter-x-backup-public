using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkAssignmentApplyService
{
    MediaBackupChunkAssignmentApplyResult Apply(IEnumerable<MediaBackupPathAssignment> assignments);
}
