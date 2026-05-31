using Backup.Application.Dump;
using Backup.Application.Dump.Models;
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
using Backup.Infrastructure.Posts.Adapters;
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
    IDumpIndexFilePolicyService dumpIndexFilePolicyService,
    IDumpLifecycleService dumpLifecycleService,
    IDumpFlushExecutionService dumpFlushExecutionService,
    IDumpReplicationPlanningService dumpReplicationPlanningService,
    IDataStoreGuardService dataStoreGuardService
) : IDumpDataStore
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<LocalDumpData> _logger = _logger;
    private readonly AppConfig _appConfig = _appConfig;
    private readonly StorageDump _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IDumpIndexFilePolicyService _dumpIndexFilePolicyService = dumpIndexFilePolicyService;
    private readonly IDumpLifecycleService _dumpLifecycleService = dumpLifecycleService;
    private readonly IDumpFlushExecutionService _dumpFlushExecutionService = dumpFlushExecutionService;
    private readonly IDumpReplicationPlanningService _dumpReplicationPlanningService =
        dumpReplicationPlanningService;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;

    private DumpData? _dumpData;
    private DumpData Data =>
        _dataStoreGuardService.RequireInitialized(_dumpData, "Dump data not initialized");

    public Task Setup() => Task.CompletedTask;

    private string GetPath(PartitionConfig partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Dumps.Paths]);

    private async Task<string> GetPathCurrent(ApiContext context)
    {
        DumpsData dumpsData = await _dumps.GetData();
        DumpCurrentSessionResolution resolution = _dumpLifecycleService.ResolveCurrentSession(
            dumpsData.Current,
            DateTime.Now
        );

        if (resolution.ShouldPersist)
        {
            dumpsData.Current = resolution.Current;
            await _dumps.Save(dumpsData);
        }

        string path = Path.Combine(
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

        return Path.Combine(path, fileName);
    }

    private async Task<string> GetPathIndex(ApiContext context)
    {
        string path = await GetPathCurrent(context);

        return Path.Combine([path, Data.Index.ToString()]);
    }

    private async Task<string> GetPathApi(ApiContext context)
    {
        string path = await GetPathIndex(context);

        return Path.Combine([path, .. _config.Paths.Dumps.Dump.Api.Paths]);
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

        Data.IndexFile++;

        string indexPath = await GetPathIndex(context);
        string apiPath = await GetPathApi(context);

        string fileName = $"{Data.IndexFile}.json";

        string indexFullPath = Path.Combine(indexPath, fileName);
        string apiFullPath = Path.Combine(apiPath, fileName);

        string indexJson = JsonConvert.SerializeObject(posts);

        await File.WriteAllTextAsync(indexFullPath, indexJson);
        await File.WriteAllTextAsync(apiFullPath, response);

        Data.Cursor = cursor;
        Data.LastUpdate = DateTime.Now;

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
        IReadOnlyList<string> indexPaths = _dumpIndexFilePolicyService.SelectIndexFiles(
            paths,
            [.. _config.Paths.Dumps.Dump.Api.Paths]
        );

        List<Post> dumpPosts = [];

        foreach (string path in indexPaths)
        {
            string content = await File.ReadAllTextAsync(path);
            List<Post>? deserialized = JsonConvert.DeserializeObject<List<Post>>(content);
            List<Post> _posts = _dataStoreGuardService.RequireDeserialized(
                deserialized,
                "Error in deserialize"
            );

            dumpPosts.AddRange(_posts);
        }

        List<Backup.Domain.Posts.Post> domainPosts = dumpPosts
            .Select(PostReplicationMapper.ToDomain)
            .ToList();

        DumpFlushExecutionResult result = await _dumpFlushExecutionService.Execute(
            new DumpFlushExecutionRequest
            {
                UserId = userId,
                Type = Data.Type ?? string.Empty,
                ContextId = context.Id,
                Posts = domainPosts,
            },
            async (sourceId, posts) =>
                await postData.AddPosts(userId, sourceId, posts.ToList()),
            async (sourceId, newPostIds) =>
                await postData.MarkDeletedExcept(userId, sourceId, newPostIds.ToList())
        );

        _logger.LogInformation("{posts} posts loaded from dump", result.LoadedCount);
        _logger.LogInformation("{posts} posts deleted", result.DeletedCount);

        DumpsData dumpsData = await _dumps.GetData();
        dumpsData.Current = null;

        await _dumps.Save(dumpsData);
    }

    private async Task Replicate(ApiContext context)
    {
        List<PartitionConfig> partitions = _partition
            .GetPartitions()
            .Except([_partition.GetPrimary()])
            .ToList();

        string mainPath = await GetPathCurrent(context);
        string primaryPath = GetPath(_partition.GetPrimary());
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
