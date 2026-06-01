using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkLoadDecisionService
{
    MediaBackupChunkLoadDecision Decide(
        string? dataFileName,
        IReadOnlyList<int>? backupChunkIds
    );
}
