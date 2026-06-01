using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkReadFailurePolicyService
{
    MediaBackupChunkReadFailureAction Decide(Exception exception, bool cancellationRequested);
}
