using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkReadFailurePolicyService
    : IMediaBackupChunkReadFailurePolicyService
{
    public MediaBackupChunkReadFailureAction Decide(Exception exception, bool cancellationRequested)
    {
        if (exception is OperationCanceledException && cancellationRequested)
            return MediaBackupChunkReadFailureAction.Throw;

        return MediaBackupChunkReadFailureAction.ReturnNull;
    }
}
