using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public sealed class DumpFlushExecutionService(IDumpFlushPlanningService dumpFlushPlanningService)
    : IDumpFlushExecutionService
{
    private readonly IDumpFlushPlanningService _dumpFlushPlanningService = dumpFlushPlanningService;

    public async Task<DumpFlushExecutionResult> Execute(
        DumpFlushExecutionRequest request,
        Func<string, IReadOnlyList<Backup.Domain.Posts.Post>, Task> addPosts,
        Func<string, IReadOnlyCollection<string>, Task<int>> markDeletedExcept
    )
    {
        DumpFlushPlan flushPlan = _dumpFlushPlanningService.Build(
            request.Type,
            request.ContextId,
            request.Posts.Select(post => post.Id)
        );

        await addPosts(flushPlan.SourceId, request.Posts);
        int deletedCount = await markDeletedExcept(flushPlan.SourceId, flushPlan.NewPostIds);

        return new DumpFlushExecutionResult
        {
            SourceId = flushPlan.SourceId,
            LoadedCount = request.Posts.Count,
            DeletedCount = deletedCount,
            NewPostIds = flushPlan.NewPostIds,
        };
    }
}
