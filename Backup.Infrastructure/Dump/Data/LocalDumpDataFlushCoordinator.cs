using Backup.Application.Dump;
using Backup.Application.Dump.Models;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Dump.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Data.Dump;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Posts.Abstractions.Data;

namespace Backup.Infrastructure.Dump.Data;

internal sealed class LocalDumpDataFlushCoordinator(
    IDumpsData dumps,
    StorageDump config,
    IDumpIndexLoadService dumpIndexLoadService,
    IDumpFlushOrchestrationService dumpFlushOrchestrationService,
    IDumpPersistenceIOService dumpPersistenceIOService,
    LocalDumpDataSessionPathResolver sessionPathResolver
)
{
    private readonly IDumpsData _dumps = dumps;
    private readonly StorageDump _config = config;
    private readonly IDumpIndexLoadService _dumpIndexLoadService = dumpIndexLoadService;
    private readonly IDumpFlushOrchestrationService _dumpFlushOrchestrationService =
        dumpFlushOrchestrationService;
    private readonly IDumpPersistenceIOService _dumpPersistenceIOService = dumpPersistenceIOService;
    private readonly LocalDumpDataSessionPathResolver _sessionPathResolver = sessionPathResolver;

    public async Task<DumpFlushOrchestrationResult> Flush(
        IPostDomainData postData,
        string userId,
        ApiContext context,
        DumpData dumpData,
        CancellationToken cancellationToken = default
    )
    {
        string currentPath = await _sessionPathResolver.GetCurrentPath(context, cancellationToken);
        IReadOnlyList<string> paths = _dumpPersistenceIOService.EnumerateJsonFiles(currentPath);
        IReadOnlyList<Backup.Domain.Posts.Post> domainPosts = await _dumpIndexLoadService.LoadPosts(
            paths,
            [.. _config.Paths.Dumps.Dump.Api.Paths]
        );
        DumpsData dumpsData = await _dumps.GetData(cancellationToken);

        DumpFlushOrchestrationResult orchestration =
            await _dumpFlushOrchestrationService.ExecuteAsync(
                userId,
                dumpData.Type ?? string.Empty,
                context.Id,
                dumpsData.Current ?? string.Empty,
                domainPosts,
                async (sourceId, posts) =>
                    await postData.AddPosts(userId, sourceId, posts.ToList()),
                async (sourceId, newPostIds) =>
                    await postData.MarkDeletedExcept(userId, sourceId, newPostIds.ToList())
            );

        dumpsData.Current = orchestration.SessionCloseResolution.Current;

        if (orchestration.SessionCloseResolution.ShouldPersist)
            await _dumps.Save(dumpsData, cancellationToken);

        return orchestration;
    }
}
