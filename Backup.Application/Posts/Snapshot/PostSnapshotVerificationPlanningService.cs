using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostSnapshotVerificationPlanningService(
    IPostHistoryLatestSelectionService postHistoryLatestSelectionService
) : IPostSnapshotVerificationPlanningService
{
    private readonly IPostHistoryLatestSelectionService _postHistoryLatestSelectionService =
        postHistoryLatestSelectionService;

    public PostSnapshotVerificationPlan Plan(
        string snapshotFileName,
        IReadOnlyList<PostHistoryPath> historyPaths
    )
    {
        PostHistoryPath? latest = _postHistoryLatestSelectionService.SelectLatest(historyPaths);

        if (latest is null)
            return new PostSnapshotVerificationPlan { ShouldCompareWithHistory = false };

        string historyFilePath = Path.Combine(latest.Path, snapshotFileName);

        return new PostSnapshotVerificationPlan
        {
            ShouldCompareWithHistory = true,
            HistoryDirectoryName = GetPathLeaf(latest.Path),
            HistoryFilePath = historyFilePath,
        };
    }

    private static string GetPathLeaf(string path)
    {
        int slashIndex = path.LastIndexOf('/');
        int backslashIndex = path.LastIndexOf('\\');
        int separatorIndex = Math.Max(slashIndex, backslashIndex);

        return separatorIndex >= 0 ? path[(separatorIndex + 1)..] : path;
    }
}
