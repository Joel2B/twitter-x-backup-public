using System.Text.RegularExpressions;
using Backup.Application.Dump;
using Backup.Application.Dump.Models;
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
    IDumpProgressPolicyService dumpProgressPolicyService
) : IDumpDataStore
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<LocalDumpData> _logger = _logger;
    private readonly AppConfig _appConfig = _appConfig;
    private readonly StorageDump _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IDumpProgressPolicyService _dumpProgressPolicyService = dumpProgressPolicyService;

    private DumpData? _dumpData;
    private DumpData Data => _dumpData ?? throw new Exception("Dump data not initialized");

    public Task Setup() => Task.CompletedTask;

    private string GetPath(PartitionConfig partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Dumps.Paths]);

    private async Task<string> GetPathCurrent(ApiContext context)
    {
        DumpsData dumpsData = await _dumps.GetData();

        string current = _dumpProgressPolicyService.EnsureCurrent(dumpsData.Current, DateTime.Now);

        if (dumpsData.Current != current)
        {
            dumpsData.Current = current;
            await _dumps.Save(dumpsData);
        }

        string path = Path.Combine(GetPath(_partition.GetPrimary()), current, context.UserId);

        Directory.CreateDirectory(path);

        return path;
    }

    private async Task<string> GetPathData(ApiContext context)
    {
        string path = await GetPathCurrent(context);

        return Path.Combine(
            path,
            _config.Paths.Dumps.Dump.File ?? throw new Exception("file not configured")
        );
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

        DumpData dump = new()
        {
            Count = _appConfig.Services.Dump.Count,
            QueryCount = Convert.ToInt32(context.Request.Query.Variables["count"]),
        };

        string content = JsonConvert.SerializeObject(dump);
        await File.WriteAllTextAsync(path, content);
    }

    private async Task SetupData(ApiContext context)
    {
        string path = await GetPathData(context);

        if (!File.Exists(path))
            throw new Exception("File doesn't exist");

        string content = await File.ReadAllTextAsync(path);
        DumpData? data = JsonConvert.DeserializeObject<DumpData>(content);

        if (data is null)
            throw new Exception("Error in deserialize");

        _dumpData = data;
    }

    private async Task SetupDirectory(ApiContext context)
    {
        DumpProgressState state = new()
        {
            Index = Data.Index,
            IndexFile = Data.IndexFile,
            Count = Data.Count,
            QueryCount = Data.QueryCount,
        };

        _dumpProgressPolicyService.AdvanceDirectoryIndex(state);
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

        if (dumpsData.Current is not null && context.Id != Data.Type)
            throw new Exception();

        Data.Type = context.Id;

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

        Regex regex = new(@"^\d+");
        string currentPath = await GetPathCurrent(context);

        List<string> paths = Directory
            .EnumerateFiles(currentPath, "*.json", SearchOption.AllDirectories)
            .ToList();

        List<Post> dumpPosts = [];

        foreach (string path in paths)
        {
            string fileName = Path.GetFileName(path);
            string apiPath = Path.Combine([.. _config.Paths.Dumps.Dump.Api.Paths, fileName]);

            if (!regex.IsMatch(fileName) || path.Contains(apiPath))
                continue;

            string content = await File.ReadAllTextAsync(path);
            List<Post>? _posts = JsonConvert.DeserializeObject<List<Post>>(content);

            if (_posts is null)
                throw new Exception("Error in deserialize");

            dumpPosts.AddRange(_posts);
        }

        string sourceId = Data.Type ?? context.Id;

        await postData.AddPosts(
            userId,
            sourceId,
            dumpPosts.Select(PostReplicationMapper.ToDomain).ToList()
        );
        _logger.LogInformation("{posts} posts loaded from dump", dumpPosts.Count);

        HashSet<string> newIds = [.. dumpPosts.Select(post => post.Id)];
        int deletedCount = await postData.MarkDeletedExcept(userId, sourceId, newIds);
        _logger.LogInformation("{posts} posts deleted", deletedCount);

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
        string relativePath = Path.GetRelativePath(primaryPath, mainPath);

        foreach (PartitionConfig partition in partitions)
        {
            string path = Path.Combine(GetPath(partition), relativePath);
            UtilsPath.CopyDirectory(mainPath, path);
        }
    }
}
