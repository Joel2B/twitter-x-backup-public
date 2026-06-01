using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostSnapshotVerificationExecutionService
{
    PostSnapshotVerificationDecision BuildDecision(
        bool verifyEnabled,
        bool snapshotFileExists,
        string snapshotFileName,
        IReadOnlyList<PostHistoryPath> historyPaths
    );

    void ValidateIfNeeded(
        PostSnapshotVerificationDecision decision,
        bool historyFileExists,
        long currentLength,
        long historyLength,
        long maxAllowedShrinkBytes
    );
}
