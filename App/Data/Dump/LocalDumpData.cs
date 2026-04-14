using System.Text.RegularExpressions;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Dump;
using Backup.App.Models.Dump;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Data.Post;

public class LocalDumpData(
    ILogger<LocalDumpData> _logger,
    Models.Config.App _appConfig,
    IDumpsData _dumps,
    IEnumerable<IPostData> _postData,
    Storage _config,
    IPartition _partition
) : IDumpData
{
    public string? Id { get; set; }

    private readonly ILogger<LocalDumpData> _logger = _logger;
    private readonly Models.Config.App _appConfig = _appConfig;
    private readonly Storage _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IEnumerable<IPostData> _postData = _postData;

    private DumpData? _dumpData;
    private DumpData Data => _dumpData ?? throw new Exception("Dump data not initialized");

    public async Task Setup()
    {
        await CreateData();
        await SetupData();
    }

    private string GetPath(Models.Config.Data.Partition partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Dumps.Paths]);

    private async Task<string> GetPathCurrent()
    {
        DumpsData dumpsData = await _dumps.GetData();

        if (dumpsData.Current is null)
        {
            dumpsData.Current = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");
            await _dumps.Save(dumpsData);
        }

        string path = Path.Combine(GetPath(_partition.GetPrimary()), dumpsData.Current);
        Directory.CreateDirectory(path);

        return path;
    }

    private async Task<string> GetPathData()
    {
        string path = await GetPathCurrent();

        return Path.Combine(
            path,
            _config.Paths.Dumps.Dump.File ?? throw new Exception("file not configured")
        );
    }

    private async Task<string> GetPathIndex()
    {
        string path = await GetPathCurrent();

        return Path.Combine([path, Data.Index.ToString()]);
    }

    private async Task<string> GetPathApi()
    {
        string path = await GetPathIndex();

        return Path.Combine([path, .. _config.Paths.Dumps.Dump.Api.Paths]);
    }

    private async Task CreateData()
    {
        string path = await GetPathData();

        if (File.Exists(path))
            return;

        string? count = _appConfig.Source.Request.Query.Variables["count"]?.ToString();

        if (count is null)
            throw new Exception("Count not configured");

        DumpData dump = new()
        {
            Count = _appConfig.Services.Dump.Count,
            QueryCount = Convert.ToInt32(count),
        };

        string content = JsonConvert.SerializeObject(dump);
        await File.WriteAllTextAsync(path, content);
    }

    private async Task SetupData()
    {
        string path = await GetPathData();

        if (!File.Exists(path))
            throw new Exception("File doesn't exist");

        string content = await File.ReadAllTextAsync(path);
        DumpData? data = JsonConvert.DeserializeObject<DumpData>(content);

        if (data is null)
            throw new Exception("Error in deserialize");

        _dumpData = data;
    }

    private async Task SetupDirectory()
    {
        int files = Data.Count / Data.QueryCount - 1;

        if (Data.IndexFile == files || Data.Index == -1)
        {
            Data.Index++;
            Data.IndexFile = -1;
        }

        string indexPath = await GetPathIndex();
        string apiPath = await GetPathApi();

        Directory.CreateDirectory(indexPath);
        Directory.CreateDirectory(apiPath);
    }

    public async Task<DumpData?> GetData()
    {
        if (_appConfig.Source.Count != -1)
            return null;

        DumpsData dumpsData = await _dumps.GetData();
        await Setup();

        if (dumpsData.Current is not null && _appConfig.Source.Id != Data.Type)
            throw new Exception();

        Data.Type = _appConfig.Source.Id;

        return Data;
    }

    public async Task Save(string response, List<Models.Post.Post> posts, string cursor)
    {
        await SetupDirectory();

        Data.IndexFile++;

        string indexPath = await GetPathIndex();
        string apiPath = await GetPathApi();

        string fileName = $"{Data.IndexFile}.json";

        string indexFullPath = Path.Combine(indexPath, fileName);
        string apiFullPath = Path.Combine(apiPath, fileName);

        string indexJson = JsonConvert.SerializeObject(posts);

        await File.WriteAllTextAsync(indexFullPath, indexJson);
        await File.WriteAllTextAsync(apiFullPath, response);

        Data.Cursor = cursor;
        Data.LastUpdate = DateTime.Now;

        await SaveData();
        await Replicate();
    }

    private async Task SaveData()
    {
        string content = JsonConvert.SerializeObject(Data);
        string path = await GetPathData();

        await File.WriteAllTextAsync(path, content);
    }

    public async Task<Dictionary<string, Models.Post.Post>> Flush(string userId)
    {
        _logger.LogInformation("dumping data");

        Regex regex = new(@"^\d+");
        string currentPath = await GetPathCurrent();

        List<string> paths = Directory
            .EnumerateFiles(currentPath, "*.json", SearchOption.AllDirectories)
            .ToList();

        List<Models.Post.Post> dumpPosts = [];

        foreach (string path in paths)
        {
            string fileName = Path.GetFileName(path);
            string apiPath = Path.Combine([.. _config.Paths.Dumps.Dump.Api.Paths, fileName]);

            if (!regex.IsMatch(fileName) || path.Contains(apiPath))
                continue;

            string content = await File.ReadAllTextAsync(path);
            List<Models.Post.Post>? _posts = JsonConvert.DeserializeObject<List<Models.Post.Post>>(
                content
            );

            if (_posts is null)
                throw new Exception("Error in deserialize");

            dumpPosts.AddRange(_posts);
        }

        IPostData postData = _postData.First();

        Dictionary<string, Models.Post.Post> merged = await postData.AddPosts(
            userId,
            Data.Type ?? "errorData.Type",
            dumpPosts
        );

        _logger.LogInformation("{posts} posts loaded from dump", dumpPosts.Count);

        HashSet<string> newIds = [.. dumpPosts.Select(post => post.Id)];

        List<Models.Post.Post> deleted = merged
            .Where(kvp => !newIds.Contains(kvp.Key))
            .Select(kvp => kvp.Value)
            .ToList();

        _logger.LogInformation("{posts} posts deleted", deleted.Count);

        foreach (Models.Post.Post post in deleted)
            post.Deleted = true;

        DumpsData dumpsData = await _dumps.GetData();
        dumpsData.Current = null;

        await _dumps.Save(dumpsData);

        return merged;
    }

    private async Task Replicate()
    {
        List<Models.Config.Data.Partition> partitions = _partition
            .GetPartitions()
            .Except([_partition.GetPrimary()])
            .ToList();

        string mainPath = await GetPathCurrent();

        foreach (Models.Config.Data.Partition partition in partitions)
        {
            string path = Path.Combine(GetPath(partition), Path.GetFileName(mainPath));
            Utils.Path.CopyDirectory(mainPath, path);
        }
    }
}
