using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostSnapshotVerificationExecutionService(
    IPostSnapshotVerificationPlanningService postSnapshotVerificationPlanningService,
    IPostSnapshotSizeGuardService postSnapshotSizeGuardService
) : IPostSnapshotVerificationExecutionService
{
    private readonly IPostSnapshotVerificationPlanningService _postSnapshotVerificationPlanningService =
        postSnapshotVerificationPlanningService;
    private readonly IPostSnapshotSizeGuardService _postSnapshotSizeGuardService =
        postSnapshotSizeGuardService;

    public PostSnapshotVerificationDecision BuildDecision(
        bool verifyEnabled,
        bool snapshotFileExists,
        string snapshotFileName,
        IReadOnlyList<PostHistoryPath> historyPaths
    )
    {
        if (!verifyEnabled || !snapshotFileExists)
            return new PostSnapshotVerificationDecision
            {
                ShouldInspectHistoryFile = false,
                SnapshotFileName = snapshotFileName,
            };

        PostSnapshotVerificationPlan plan = _postSnapshotVerificationPlanningService.Plan(
            snapshotFileName,
            historyPaths
        );

        if (!plan.ShouldCompareWithHistory)
            return new PostSnapshotVerificationDecision
            {
                ShouldInspectHistoryFile = false,
                SnapshotFileName = snapshotFileName,
            };

        return new PostSnapshotVerificationDecision
        {
            ShouldInspectHistoryFile = true,
            SnapshotFileName = snapshotFileName,
            HistoryDirectoryName = plan.HistoryDirectoryName,
            HistoryFilePath = plan.HistoryFilePath,
        };
    }

    public void ValidateIfNeeded(
        PostSnapshotVerificationDecision decision,
        bool historyFileExists,
        long currentLength,
        long historyLength,
        long maxAllowedShrinkBytes
    )
    {
        if (!decision.ShouldInspectHistoryFile)
            return;

        if (!historyFileExists)
            return;

        _postSnapshotSizeGuardService.EnsureNotShrunkBeyondThreshold(
            currentLength,
            historyLength,
            maxAllowedShrinkBytes,
            decision.SnapshotFileName,
            decision.HistoryDirectoryName
        );
    }
}
