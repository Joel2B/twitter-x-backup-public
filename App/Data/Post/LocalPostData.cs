using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Post;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Data.Post;

public class LocalPostData(
    ILogger<LocalPostData> _logger,
    Models.Config.App _appConfig,
    Storage _config,
    IPartition _partition
) : IPostData, ISetup
{
    public string? Id { get; set; }

    private readonly ILogger<LocalPostData> _logger = _logger;
    private readonly Models.Config.App _appConfig = _appConfig;
    private readonly Storage _config = _config;
    private readonly IPartition _partition = _partition;

    public Task Setup()
    {
        SetupDirectory();

        return Task.CompletedTask;
    }

    private void SetupDirectory()
    {
        foreach (Models.Config.Data.Partition partition in _partition.GetPartitions())
            Directory.CreateDirectory(GetPath(partition));
    }

    private string GetPath(Models.Config.Data.Partition partition) =>
        Utils.Path.GetPath(
            [.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Post.Paths]
        );

    private string GetPathFile(Models.Config.Data.Partition? partition = null)
    {
        if (_config.Paths.Post.File is null)
            throw new Exception("file not configured");

        Models.Config.Data.Partition primary = partition ?? _partition.GetPrimary();
        string path = Path.Combine(GetPath(primary), _config.Paths.Post.File);

        return path;
    }

    public async Task<List<Models.Post.Post>?> GetAll()
    {
        string path = GetPathFile();

        if (!File.Exists(path))
            return null;

        await Verify();

        string content = await File.ReadAllTextAsync(path);

        List<Models.Post.Post>? posts =
            JsonConvert.DeserializeObject<List<Models.Post.Post>>(content)
            ?? throw new Exception("Error deserializing the file.");

        return posts;
    }

    private async Task Verify()
    {
        if (!_config.Tasks.Verify)
            return;

        string basePath = GetPath(_partition.GetPrimary());

        var fileLarger = Directory
            .GetFiles(basePath, "*.json", SearchOption.TopDirectoryOnly)
            .Where(o => Utils.Path.ToDate(o) is not null)
            .Select(o => new { Path = o, new FileInfo(o).Length })
            .MaxBy(o => o.Length);

        if (fileLarger is null)
            throw new Exception();

        string path = GetPathFile();
        long length = new FileInfo(path).Length;

        if (length < fileLarger.Length)
            if (_config.Tasks.Fix)
                _logger.LogWarning("files of different sizes");
            else
                throw new Exception("files of different sizes");

        if (!_config.Tasks.Fix)
            return;

        string content = await File.ReadAllTextAsync(path);

        List<Models.Post.Post>? posts =
            JsonConvert.DeserializeObject<List<Models.Post.Post>>(content)
            ?? throw new Exception("Error deserializing the file.");

        string contentLarger = await File.ReadAllTextAsync(fileLarger.Path);

        List<Models.Post.Post>? postsLarger =
            JsonConvert.DeserializeObject<List<Models.Post.Post>>(contentLarger)
            ?? throw new Exception("Error deserializing the file.");

        Dictionary<string, Models.Post.Post> dictPosts = new(
            capacity: postsLarger.Count,
            comparer: StringComparer.Ordinal
        );

        HashSet<string> dups = [];

        foreach (Models.Post.Post post in postsLarger)
            if (!dictPosts.TryAdd(post.Id, post))
                dups.Add(post.Id);

        _logger.LogInformation("repeated posts: {count}", dups.Count);

        if (dups.Count > 0)
            throw new Exception();

        int added = 0;
        int edited = 0;

        foreach (Models.Post.Post post in posts)
        {
            if (dictPosts.TryAdd(post.Id, post))
            {
                postsLarger.Add(post);
                added++;
            }
            else
            {
                bool indexEdited = false;

                foreach (var kvp in post.Index)
                    if (!dictPosts[post.Id].Index.ContainsKey(kvp.Key))
                    {
                        dictPosts[post.Id].Index[kvp.Key] = kvp.Value;
                        indexEdited = true;

                        _logger.LogInformation(
                            "index: {key}, value: {value}",
                            kvp.Key,
                            JsonConvert.SerializeObject(kvp.Value)
                        );
                    }

                if (indexEdited)
                {
                    _logger.LogInformation("post: {id}", post.Id);
                    edited++;
                }
            }
        }

        await Save([.. dictPosts.Values]);
        _logger.LogInformation("added: {added}, edited: {edited}", added, edited);
    }

    public async Task<Dictionary<string, Models.Post.Post>?> GetAllAsDictionary()
    {
        List<Models.Post.Post>? posts = await GetAll();

        return posts?.ToDictionary(post => post.Id);
    }

    public async Task Save(List<Models.Post.Post> posts)
    {
        string path = GetPathFile();

        RenameFile();

        string data = JsonConvert.SerializeObject(posts);
        await File.WriteAllTextAsync(path, data);

        await SaveFormatted(posts);
        Replicate();
    }

    private async Task SaveFormatted(List<Models.Post.Post> posts)
    {
        string path = Utils.Path.GetPathFormatted(GetPathFile());
        string json = JsonConvert.SerializeObject(posts, Formatting.Indented);

        await File.WriteAllTextAsync(path, json);
    }

    private void RenameFile()
    {
        string path = GetPathFile();

        if (!File.Exists(path))
            return;

        string date = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");
        string fileName = $"{date}.json";
        string newPath = path.Replace(Path.GetFileName(path), fileName);

        File.Move(path, newPath);
    }

    public async Task Prune()
    {
        _logger.LogInformation("running prune");
        _logger.LogInformation("prune: {value}", _config.Tasks.Prune);

        if (!_config.Tasks.Prune)
            return;

        foreach (Models.Config.Data.Partition partition in _partition.GetPartitions())
            await PrunePartition(partition);
    }

    private Task PrunePartition(Models.Config.Data.Partition partition)
    {
        _logger.LogInformation("prunning partition: {value}", partition.Id);

        string basePath = GetPath(partition);
        string[] pathsFiles = Directory.GetFiles(basePath, "*.json", SearchOption.TopDirectoryOnly);
        _logger.LogInformation("base path: {path}", Path.GetFileName(basePath));

        var pathsDate = pathsFiles
            .Where(o => Utils.Path.ToDate(o) is not null)
            .Select(o => new { Path = o, Date = Utils.Path.ToDate(o) })
            .GroupBy(o => Convert.ToDateTime(o.Date?.ToString("yyyy-MM-dd")))
            .OrderBy(o => o.Key)
            .ToList();

        _logger.LogInformation("paths: {value}", pathsDate.Count);

        if (pathsDate.Count == 0)
            return Task.CompletedTask;

        DateTime? date = pathsDate
            .Last()
            .Last()
            .Date?.AddDays(-_appConfig.Tasks.Prune.Data.Post.KeepDays)
            .Date;

        _logger.LogInformation(
            "prunning date: {date}, KeepDays: {keepDays}",
            date,
            _appConfig.Tasks.Prune.Data.Post.KeepDays
        );

        if (date is null)
            return Task.CompletedTask;

        var groupBefore = pathsDate.Where(o => o.Key < date).ToList();

        var groupAfter = pathsDate
            .Where(o => o.Key >= date)
            .Select(o => new
            {
                o.Key,
                Paths = o.Take(o.Count() - _appConfig.Tasks.Prune.Data.Post.KeepCount),
            })
            .ToList();

        List<string> remove = [];
        List<string> removeBefore = groupBefore.SelectMany(o => o.Select(o => o.Path)).ToList();
        List<string> removeAfter = groupAfter.SelectMany(o => o.Paths.Select(o => o.Path)).ToList();

        remove.AddRange(removeBefore);
        remove.AddRange(removeAfter);

        _logger.LogInformation("prunning {value} paths", remove.Count);

        if (remove.Count == 0)
            return Task.CompletedTask;

        foreach (string path in remove)
        {
            File.Delete(path);
            _logger.LogInformation("{path} removed", Path.GetFileName(path));
        }

        return Task.CompletedTask;
    }

    public void Replicate()
    {
        List<Models.Config.Data.Partition> partitions = _partition
            .GetPartitions()
            .Except([_partition.GetPrimary()])
            .ToList();

        string mainPath = GetPathFile();
        string mainPathFormatted = Utils.Path.GetPathFormatted(mainPath);

        foreach (Models.Config.Data.Partition partition in partitions)
        {
            string path = GetPathFile(partition);
            string pathFormatted = Utils.Path.GetPathFormatted(path);

            File.Delete(path);
            File.Copy(mainPath, path);

            File.Delete(pathFormatted);
            File.Copy(mainPathFormatted, pathFormatted);
        }
    }
}
