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
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Dump.Data;

public class LocalDumpData(
    ILogger<LocalDumpData> _logger,
    AppConfig _appConfig,
    IDumpsData _dumps,
    StorageDump _config,
    IPartition _partition,
    LocalDumpDataDependencies dependencies
) : IDumpDataStore
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<LocalDumpData> _logger = _logger;
    private readonly AppConfig _appConfig = _appConfig;
    private readonly StorageDump _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly ISecondaryStoreSelectionService _secondaryStoreSelectionService =
        dependencies.SecondaryStoreSelectionService;
    private readonly IDumpContextEligibilityService _dumpContextEligibilityService =
        dependencies.DumpContextEligibilityService;
    private readonly IDumpLifecycleService _dumpLifecycleService = dependencies.DumpLifecycleService;
    private readonly IDumpPathService _dumpPathService = dependencies.DumpPathService;
    private readonly IDumpIndexLoadService _dumpIndexLoadService = dependencies.DumpIndexLoadService;
    private readonly IDumpSaveExecutionService _dumpSaveExecutionService =
        dependencies.DumpSaveExecutionService;
    private readonly IDumpFlushOrchestrationService _dumpFlushOrchestrationService =
        dependencies.DumpFlushOrchestrationService;
    private readonly IDumpReplicationPlanningService _dumpReplicationPlanningService =
        dependencies.DumpReplicationPlanningService;
    private readonly IDumpPersistenceIOService _dumpPersistenceIOService =
        dependencies.DumpPersistenceIOService;
    private readonly IDataStoreGuardService _dataStoreGuardService =
        dependencies.DataStoreGuardService;
    private readonly IDateTimeProvider _dateTimeProvider = dependencies.DateTimeProvider;

    private DumpData? _dumpData;
    private DumpData Data =>
        _dataStoreGuardService.RequireInitialized(_dumpData, "Dump data not initialized");

    public Task Setup() => Task.CompletedTask;

    private string GetPath(PartitionConfig partition) =>
        _dumpPathService.BuildDumpRootPath(
            partition.Paths,
            _config.Paths.Paths,
            _config.Paths.Dumps.Paths
        );

    private async Task<string> GetPathCurrent(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        DumpsData dumpsData = await _dumps.GetData(cancellationToken);
        DumpCurrentSessionResolution resolution = _dumpLifecycleService.ResolveCurrentSession(
            dumpsData.Current,
            _dateTimeProvider.Now
        );

        if (resolution.ShouldPersist)
        {
            dumpsData.Current = resolution.Current;
            await _dumps.Save(dumpsData, cancellationToken);
        }

        string path = _dumpPathService.BuildCurrentUserPath(
            GetPath(_partition.GetPrimary()),
            resolution.Current,
            context.UserId
        );

        Directory.CreateDirectory(path);

        return path;
    }

    private async Task<string> GetPathData(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        string path = await GetPathCurrent(context, cancellationToken);
        string fileName = _dataStoreGuardService.RequireConfiguredFileName(
            _config.Paths.Dumps.Dump.File
        );

        return _dumpPathService.BuildDataFilePath(path, fileName);
    }

    private async Task<string> GetPathIndex(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        string path = await GetPathCurrent(context, cancellationToken);

        return _dumpPathService.BuildIndexPath(path, Data.Index);
    }

    private async Task<string> GetPathApi(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        string path = await GetPathIndex(context, cancellationToken);

        return _dumpPathService.BuildApiPath(path, _config.Paths.Dumps.Dump.Api.Paths);
    }

    private async Task CreateData(ApiContext context, CancellationToken cancellationToken = default)
    {
        string path = await GetPathData(context, cancellationToken);

        if (File.Exists(path))
            return;

        DumpDataInitialization initialized = _dumpLifecycleService.CreateInitialData(
            _appConfig.Services.Dump.Count,
            context.Request.Query.Variables["count"]
        );

        DumpData dump = new() { Count = initialized.Count, QueryCount = initialized.QueryCount };
        await _dumpPersistenceIOService.WriteDumpData(path, dump, cancellationToken);
    }

    private async Task SetupData(ApiContext context, CancellationToken cancellationToken = default)
    {
        string path = await GetPathData(context, cancellationToken);

        _dataStoreGuardService.EnsureFileExists(path);

        DumpData? deserialized = await _dumpPersistenceIOService.ReadDumpData(
            path,
            cancellationToken
        );
        DumpData data = _dataStoreGuardService.RequireDeserialized(
            deserialized,
            "Error in deserialize"
        );

        _dumpData = data;
    }

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

        string indexPath = await GetPathIndex(context, cancellationToken);
        string apiPath = await GetPathApi(context, cancellationToken);

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
        await CreateData(context, cancellationToken);
        await SetupData(context, cancellationToken);

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

        string indexPath = await GetPathIndex(context, cancellationToken);
        string apiPath = await GetPathApi(context, cancellationToken);

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
    {
        string path = await GetPathData(context, cancellationToken);
        await _dumpPersistenceIOService.WriteDumpData(path, Data, cancellationToken);
    }

    public async Task Flush(
        IPostDomainData postData,
        string userId,
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("dumping data");

        string currentPath = await GetPathCurrent(context, cancellationToken);

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
    {
        PartitionConfig primary = _partition.GetPrimary();
        IReadOnlyList<PartitionConfig> partitions =
            _secondaryStoreSelectionService.SelectSecondaries(_partition.GetPartitions(), primary);

        string mainPath = await GetPathCurrent(context, cancellationToken);
        string primaryPath = GetPath(primary);
        DumpReplicationPlan plan = _dumpReplicationPlanningService.Plan(
            primaryPath,
            mainPath,
            partitions.Select(GetPath)
        );

        foreach (string path in plan.TargetPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _dumpPersistenceIOService.CopyDirectory(mainPath, path);
        }
    }
}
