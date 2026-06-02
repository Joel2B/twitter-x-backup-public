using Backup.Application.Core;
using Backup.Application.Dump;
using Backup.Application.Dump.Models;
using Backup.Application.IO;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Dump.Abstractions.Services;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Bulk;
using Backup.Infrastructure.Models.Config.Data.Dump;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Dump.Data;

public class LocalDumpData : IDumpDataStore
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<LocalDumpData> _logger;
    private readonly IDumpsData _dumps;
    private readonly StorageDump _config;
    private readonly IDumpContextEligibilityService _dumpContextEligibilityService;
    private readonly IDumpLifecycleService _dumpLifecycleService;
    private readonly IDumpIndexLoadService _dumpIndexLoadService;
    private readonly IDumpSaveExecutionService _dumpSaveExecutionService;
    private readonly IDumpFlushOrchestrationService _dumpFlushOrchestrationService;
    private readonly IDataStoreGuardService _dataStoreGuardService;
    private readonly IDumpPersistenceIOService _dumpPersistenceIOService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly LocalDumpDataPathLayout _pathLayout;
    private readonly LocalDumpDataSessionPathResolver _sessionPathResolver;
    private readonly LocalDumpDataStateCoordinator _stateCoordinator;
    private readonly LocalDumpDataReplicationCoordinator _replicationCoordinator;

    private DumpData? _dumpData;
    private DumpData Data =>
        _dataStoreGuardService.RequireInitialized(_dumpData, "Dump data not initialized");

    public LocalDumpData(
        ILogger<LocalDumpData> logger,
        AppConfig appConfig,
        IDumpsData dumps,
        StorageDump config,
        IPartition partition,
        LocalDumpDataDependencies dependencies
    )
    {
        _logger = logger;
        _dumps = dumps;
        _config = config;
        _dumpContextEligibilityService = dependencies.DumpContextEligibilityService;
        _dumpLifecycleService = dependencies.DumpLifecycleService;
        _dumpIndexLoadService = dependencies.DumpIndexLoadService;
        _dumpSaveExecutionService = dependencies.DumpSaveExecutionService;
        _dumpFlushOrchestrationService = dependencies.DumpFlushOrchestrationService;
        _dataStoreGuardService = dependencies.DataStoreGuardService;
        _dumpPersistenceIOService = dependencies.DumpPersistenceIOService;
        _dateTimeProvider = dependencies.DateTimeProvider;
        _pathLayout = new(
            config,
            partition,
            dependencies.DumpPathService,
            dependencies.DataStoreGuardService
        );
        _sessionPathResolver = new(
            dumps,
            dependencies.DumpLifecycleService,
            dependencies.DateTimeProvider,
            _pathLayout
        );
        _stateCoordinator = new(
            appConfig,
            dependencies.DumpLifecycleService,
            dependencies.DataStoreGuardService,
            dependencies.DumpPersistenceIOService,
            _sessionPathResolver
        );
        _replicationCoordinator = new(
            dependencies.SecondaryStoreSelectionService,
            partition,
            dependencies.DumpReplicationPlanningService,
            dependencies.DumpPersistenceIOService,
            _pathLayout,
            _sessionPathResolver
        );
    }

    public Task Setup() => Task.CompletedTask;

    private async Task<DumpSaveExecutionResult> SetupDirectoryForSave(
        ApiContext context,
        string cursor,
        CancellationToken cancellationToken = default
    )
    {
        DumpSaveExecutionResult result = _dumpSaveExecutionService.Execute(
            Data.Index,
            Data.IndexFile,
            Data.Count,
            Data.QueryCount,
            cursor,
            _dateTimeProvider.Now
        );
        Data.Index = result.DirectoryState.Index;
        Data.IndexFile = result.SaveState.IndexFile;

        string indexPath = await _sessionPathResolver.GetIndexPath(
            context,
            Data.Index,
            cancellationToken
        );
        string apiPath = await _sessionPathResolver.GetApiPath(
            context,
            Data.Index,
            cancellationToken
        );

        Directory.CreateDirectory(indexPath);
        Directory.CreateDirectory(apiPath);

        return result;
    }

    public async Task<DumpData?> GetData(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (!_dumpContextEligibilityService.ShouldLoadDumpData(context.Count))
            return null;

        DumpsData dumpsData = await _dumps.GetData(cancellationToken);
        _dumpData = await _stateCoordinator.Load(context, cancellationToken);

        Data.Type = _dumpLifecycleService.ResolveType(dumpsData.Current, context.Id, Data.Type);

        return Data;
    }

    public async Task Save(
        string response,
        List<Post> posts,
        string cursor,
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        DumpSaveExecutionResult saveExecution = await SetupDirectoryForSave(
            context,
            cursor,
            cancellationToken
        );

        string indexPath = await _sessionPathResolver.GetIndexPath(
            context,
            Data.Index,
            cancellationToken
        );
        string apiPath = await _sessionPathResolver.GetApiPath(
            context,
            Data.Index,
            cancellationToken
        );

        string fileName = saveExecution.FileName;

        string indexFullPath = Path.Combine(indexPath, fileName);
        string apiFullPath = Path.Combine(apiPath, fileName);

        await _dumpPersistenceIOService.WritePostsIndex(indexFullPath, posts, cancellationToken);
        await _dumpPersistenceIOService.WriteApiResponse(apiFullPath, response, cancellationToken);

        Data.Cursor = saveExecution.SaveState.Cursor;
        Data.LastUpdate = saveExecution.SaveState.LastUpdate;

        await SaveData(context, cancellationToken);
        await Replicate(context, cancellationToken);
    }

    private async Task SaveData(ApiContext context, CancellationToken cancellationToken = default)
    => await _stateCoordinator.Save(context, Data, cancellationToken);

    public async Task Flush(
        IPostDomainData postData,
        string userId,
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("dumping data");

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
                Data.Type ?? string.Empty,
                context.Id,
                dumpsData.Current ?? string.Empty,
                domainPosts,
                async (sourceId, posts) =>
                    await postData.AddPosts(userId, sourceId, posts.ToList()),
                async (sourceId, newPostIds) =>
                    await postData.MarkDeletedExcept(userId, sourceId, newPostIds.ToList())
            );

        _logger.LogInformation(
            "{posts} posts loaded from dump",
            orchestration.FlushResult.LoadedCount
        );
        _logger.LogInformation("{posts} posts deleted", orchestration.FlushResult.DeletedCount);

        dumpsData.Current = orchestration.SessionCloseResolution.Current;

        if (orchestration.SessionCloseResolution.ShouldPersist)
            await _dumps.Save(dumpsData, cancellationToken);
    }

    private async Task Replicate(ApiContext context, CancellationToken cancellationToken = default)
        => await _replicationCoordinator.Replicate(context, cancellationToken);
}
