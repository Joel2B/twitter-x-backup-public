using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public sealed class DumpFlushOrchestrationService(
    IDumpFlushRequestFactoryService dumpFlushRequestFactoryService,
    IDumpFlushExecutionService dumpFlushExecutionService,
    IDumpLifecycleService dumpLifecycleService
) : IDumpFlushOrchestrationService
{
    private readonly IDumpFlushRequestFactoryService _dumpFlushRequestFactoryService =
        dumpFlushRequestFactoryService;
    private readonly IDumpFlushExecutionService _dumpFlushExecutionService = dumpFlushExecutionService;
    private readonly IDumpLifecycleService _dumpLifecycleService = dumpLifecycleService;

    public async Task<DumpFlushOrchestrationResult> ExecuteAsync(
        string userId,
        string dataType,
        string sourceId,
        string currentSession,
        IReadOnlyList<Backup.Domain.Posts.Post> domainPosts,
        Func<string, IReadOnlyList<Backup.Domain.Posts.Post>, Task> addPosts,
        Func<string, IReadOnlyCollection<string>, Task<int>> markDeletedExcept,
        CancellationToken cancellationToken = default
    )
    {
        DumpFlushExecutionRequest request = _dumpFlushRequestFactoryService.Build(
            userId,
            dataType,
            sourceId,
            domainPosts
        );

        DumpFlushExecutionResult flushResult = await _dumpFlushExecutionService.Execute(
            request,
            addPosts,
            markDeletedExcept
        );

        DumpSessionCloseResolution closeResolution = _dumpLifecycleService.ResolveSessionClose(
            currentSession
        );

        return new DumpFlushOrchestrationResult
        {
            FlushResult = flushResult,
            SessionCloseResolution = closeResolution,
        };
    }
}
