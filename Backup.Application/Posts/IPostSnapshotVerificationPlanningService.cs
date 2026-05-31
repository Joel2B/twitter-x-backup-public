using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostSnapshotVerificationPlanningService
{
    PostSnapshotVerificationPlan Plan(
        string snapshotFileName,
        IReadOnlyList<PostHistoryPath> historyPaths
    );
}
