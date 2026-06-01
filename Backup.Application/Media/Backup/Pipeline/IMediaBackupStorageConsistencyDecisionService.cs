using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupStorageConsistencyDecisionService
{
    MediaBackupStorageConsistencyDecision DecideForApply(
        IEnumerable<string> expectedPaths,
        IEnumerable<string> actualPaths
    );

    MediaBackupStorageConsistencyDecision DecideForDuplicateCheck(
        IEnumerable<string> expectedPaths,
        IEnumerable<string> actualPaths
    );
}
