using Backup.Application.Core;
using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Infrastructure.Posts.Data.Json;

internal sealed class LocalPostDataHistoryCoordinator(
    IPostHistoryPathExtractionService postHistoryPathExtractionService,
    IPostHistoryPrunePlanningService postHistoryPrunePlanningService,
    IPostSnapshotVerificationExecutionService postSnapshotVerificationExecutionService,
    IPostDataReplicationPlanningService postDataReplicationPlanningService,
    IPostHistoryArchivePathService postHistoryArchivePathService,
    IDateTimeProvider dateTimeProvider
)
{
    private readonly IPostHistoryPathExtractionService _postHistoryPathExtractionService =
        postHistoryPathExtractionService;
    private readonly IPostHistoryPrunePlanningService _postHistoryPrunePlanningService =
        postHistoryPrunePlanningService;
    private readonly IPostSnapshotVerificationExecutionService _postSnapshotVerificationExecutionService =
        postSnapshotVerificationExecutionService;
    private readonly IPostDataReplicationPlanningService _postDataReplicationPlanningService =
        postDataReplicationPlanningService;
    private readonly IPostHistoryArchivePathService _postHistoryArchivePathService =
        postHistoryArchivePathService;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public IReadOnlyList<PostHistoryPath> ExtractHistoryPaths(IEnumerable<string> paths) =>
        _postHistoryPathExtractionService.Extract(paths);

    public PostSnapshotVerificationDecision BuildSnapshotDecision(
        bool verifyEnabled,
        bool currentExists,
        string currentFileName,
        IReadOnlyList<PostHistoryPath> historyPaths
    ) =>
        _postSnapshotVerificationExecutionService.BuildDecision(
            verifyEnabled,
            currentExists,
            currentFileName,
            historyPaths
        );

    public void ValidateSnapshotIfNeeded(
        PostSnapshotVerificationDecision decision,
        bool historyExists,
        long currentLength,
        long historyLength,
        long maxSizeDiffBytes
    ) =>
        _postSnapshotVerificationExecutionService.ValidateIfNeeded(
            decision,
            historyExists,
            currentLength,
            historyLength,
            maxSizeDiffBytes
        );

    public IReadOnlyList<PostDataFileReplicationOperation> PlanReplication(
        IReadOnlyList<string> sourcePaths,
        IReadOnlyList<string> targetPaths
    ) => _postDataReplicationPlanningService.Plan(sourcePaths, targetPaths);

    public string ResolveUniqueHistoryDirectoryPath(
        string basePath,
        string legacyDateFormat,
        Func<string, bool> pathExists
    ) =>
        _postHistoryArchivePathService.ResolveUniqueHistoryDirectoryPath(
            basePath,
            _dateTimeProvider.Now,
            legacyDateFormat,
            pathExists
        );

    public PostHistoryPrunePlan PlanPrune(
        IReadOnlyList<PostHistoryPath> historyPaths,
        int keepDays,
        int keepCount
    ) => _postHistoryPrunePlanningService.Plan(historyPaths, keepDays, keepCount);
}
