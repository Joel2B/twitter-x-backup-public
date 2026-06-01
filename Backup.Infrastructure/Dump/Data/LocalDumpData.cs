using Backup.Application.Dump;
using Backup.Application.Dump.Models;
using Backup.Application.Core;
using Backup.Application.IO;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Bulk;
using Backup.Infrastructure.Models.Config.Data.Dump;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Posts.Models;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Dump.Data;

public class LocalDumpData(
    ILogger<LocalDumpData> _logger,
    AppConfig _appConfig,
    IDumpsData _dumps,
    StorageDump _config,
    IPartition _partition,
    ISecondaryStoreSelectionService secondaryStoreSelectionService,
    IDumpLifecycleService dumpLifecycleService,
    IDumpPathService dumpPathService,
    IDumpIndexLoadService dumpIndexLoadService,
    IDumpFlushRequestFactoryService dumpFlushRequestFactoryService,
    IDumpFlushExecutionService dumpFlushExecutionService,
    IDumpReplicationPlanningService dumpReplicationPlanningService,
    IDataStoreGuardService dataStoreGuardService,
    IDateTimeProvider dateTimeProvider
) : IDumpDataStore
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<LocalDumpData> _logger = _logger;
    private readonly AppConfig _appConfig = _appConfig;
    private readonly StorageDump _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly ISecondaryStoreSelectionService _secondaryStoreSelectionService =
        secondaryStoreSelectionService;
    private readonly IDumpLifecycleService _dumpLifecycleService = dumpLifecycleService;
    private readonly IDumpPathService _dumpPathService = dumpPathService;
    private readonly IDumpIndexLoadService _dumpIndexLoadService = dumpIndexLoadService;
    private readonly IDumpFlushRequestFactoryService _dumpFlushRequestFactoryService =
        dumpFlushRequestFactoryService;
    private readonly IDumpFlushExecutionService _dumpFlushExecutionService = dumpFlushExecutionService;
    private readonly IDumpReplicationPlanningService _dumpReplicationPlanningService =
        dumpReplicationPlanningService;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    private DumpData? _dumpData;
    private DumpData Data =>
        _dataStoreGuardService.RequireInitialized(_dumpData, "Dump data not initialized");

    public Task Setup() => Task.CompletedTask;

    private string GetPath(PartitionConfig partition) =>
        _dumpPathService.BuildDumpRootPath(partition.Paths, _config.Paths.Paths, _config.Paths.Dumps.Paths);

    private async Task<string> GetPathCurrent(ApiContext context)
    {
        DumpsData dumpsData = await _dumps.GetData();
        DumpCurrentSessionResolution resolution = _dumpLifecycleService.ResolveCurrentSession(
            dumpsData.Current,
            _dateTimeProvider.Now
        );

        if (resolution.ShouldPersist)
        {
            dumpsData.Current = resolution.Current;
            await _dumps.Save(dumpsData);
        }

        string path = _dumpPathService.BuildCurrentUserPath(
            GetPath(_partition.GetPrimary()),
            resolution.Current,
            context.UserId
        );

        Directory.CreateDirectory(path);

        return path;
    }

    private async Task<string> GetPathData(ApiContext context)
    {
        string path = await GetPathCurrent(context);
        string fileName = _dataStoreGuardService.RequireConfiguredFileName(_config.Paths.Dumps.Dump.File);

        return _dumpPathService.BuildDataFilePath(path, fileName);
    }

    private async Task<string> GetPathIndex(ApiContext context)
    {
        string path = await GetPathCurrent(context);

        return _dumpPathService.BuildIndexPath(path, Data.Index);
    }

    private async Task<string> GetPathApi(ApiContext context)
    {
        string path = await GetPathIndex(context);

        return _dumpPathService.BuildApiPath(path, _config.Paths.Dumps.Dump.Api.Paths);
    }

    private async Task CreateData(ApiContext context)
    {
        string path = await GetPathData(context);

        if (File.Exists(path))
            return;

        DumpDataInitialization initialized = _dumpLifecycleService.CreateInitialData(
            _appConfig.Services.Dump.Count,
            context.Request.Query.Variables["count"]
        );

        DumpData dump = new()
        {
            Count = initialized.Count,
            QueryCount = initialized.QueryCount,
        };

        string content = JsonConvert.SerializeObject(dump);
        await File.WriteAllTextAsync(path, content);
    }

    private async Task SetupData(ApiContext context)
    {
        string path = await GetPathData(context);

        _dataStoreGuardService.EnsureFileExists(path);

        string content = await File.ReadAllTextAsync(path);
        DumpData? deserialized = JsonConvert.DeserializeObject<DumpData>(content);
        DumpData data = _dataStoreGuardService.RequireDeserialized(
            deserialized,
            "Error in deserialize"
        );

        _dumpData = data;
    }

    private async Task SetupDirectory(ApiContext context)
    {
        DumpProgressState state = _dumpLifecycleService.AdvanceDirectory(
            Data.Index,
            Data.IndexFile,
            Data.Count,
            Data.QueryCount
        );
        Data.Index = state.Index;
        Data.IndexFile = state.IndexFile;

        string indexPath = await GetPathIndex(context);
        string apiPath = await GetPathApi(context);

        Directory.CreateDirectory(indexPath);
        Directory.CreateDirectory(apiPath);
    }

    public async Task<DumpData?> GetData(ApiContext context)
    {
        if (context.Count != -1)
            return null;

        DumpsData dumpsData = await _dumps.GetData();
        await CreateData(context);
        await SetupData(context);

        Data.Type = _dumpLifecycleService.ResolveType(dumpsData.Current, context.Id, Data.Type);

        return Data;
    }

    public async Task Save(string response, List<Post> posts, string cursor, ApiContext context)
    {
        await SetupDirectory(context);
        DumpSaveProgressState saveState = _dumpLifecycleService.AdvanceSave(
            Data.IndexFile,
            cursor,
            _dateTimeProvider.Now
        );
        Data.IndexFile = saveState.IndexFile;

        string indexPath = await GetPathIndex(context);
        string apiPath = await GetPathApi(context);

        string fileName = _dumpPathService.BuildIndexFileName(Data.IndexFile);

        string indexFullPath = Path.Combine(indexPath, fileName);
        string apiFullPath = Path.Combine(apiPath, fileName);

        string indexJson = JsonConvert.SerializeObject(posts);

        await File.WriteAllTextAsync(indexFullPath, indexJson);
        await File.WriteAllTextAsync(apiFullPath, response);

        Data.Cursor = saveState.Cursor;
        Data.LastUpdate = saveState.LastUpdate;

        await SaveData(context);
        await Replicate(context);
    }

    private async Task SaveData(ApiContext context)
    {
        string content = JsonConvert.SerializeObject(Data);
        string path = await GetPathData(context);

        await File.WriteAllTextAsync(path, content);
    }

    public async Task Flush(IPostDomainData postData, string userId, ApiContext context)
    {
        _logger.LogInformation("dumping data");

        string currentPath = await GetPathCurrent(context);

        List<string> paths = Directory
            .EnumerateFiles(currentPath, "*.json", SearchOption.AllDirectories)
            .ToList();
        IReadOnlyList<Backup.Domain.Posts.Post> domainPosts = await _dumpIndexLoadService.LoadPosts(
            paths,
            [.. _config.Paths.Dumps.Dump.Api.Paths]
        );
        DumpFlushExecutionRequest request = _dumpFlushRequestFactoryService.Build(
            userId,
            Data.Type,
            context.Id,
            domainPosts
        );

        DumpFlushExecutionResult result = await _dumpFlushExecutionService.Execute(
            request,
            async (sourceId, posts) =>
                await postData.AddPosts(userId, sourceId, posts.ToList()),
            async (sourceId, newPostIds) =>
                await postData.MarkDeletedExcept(userId, sourceId, newPostIds.ToList())
        );

        _logger.LogInformation("{posts} posts loaded from dump", result.LoadedCount);
        _logger.LogInformation("{posts} posts deleted", result.DeletedCount);

        DumpsData dumpsData = await _dumps.GetData();
        DumpSessionCloseResolution closeResolution = _dumpLifecycleService.ResolveSessionClose(
            dumpsData.Current
        );
        dumpsData.Current = closeResolution.Current;

        if (closeResolution.ShouldPersist)
            await _dumps.Save(dumpsData);
    }

    private async Task Replicate(ApiContext context)
    {
        PartitionConfig primary = _partition.GetPrimary();
        IReadOnlyList<PartitionConfig> partitions = _secondaryStoreSelectionService.SelectSecondaries(
            _partition.GetPartitions(),
            primary
        );

        string mainPath = await GetPathCurrent(context);
        string primaryPath = GetPath(primary);
        DumpReplicationPlan plan = _dumpReplicationPlanningService.Plan(
            primaryPath,
            mainPath,
            partitions.Select(GetPath)
        );

        foreach (string path in plan.TargetPaths)
        {
            UtilsPath.CopyDirectory(mainPath, path);
        }
    }
}
