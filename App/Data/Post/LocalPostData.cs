using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Post;
using Backup.App.Models.Data.Json;
using Microsoft.Extensions.Logging;

namespace Backup.App.Data.Post;

public partial class LocalPostData(
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

    private Dictionary<string, Models.Post.Post>? _postsCache = null;

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

    private async Task<Dictionary<string, Models.Post.Post>?> GetCache()
    {
        if (_postsCache is not null)
            return _postsCache;

        PrepareTablesDirectories();
        string normalizedPostsPath = GetCurrentTablesFilePath(NormalizedPostsFileName);

        if (!File.Exists(normalizedPostsPath))
            return null;

        await Verify();

        LocalPostTables tables = await LoadTables();
        List<Models.Post.Post> posts = BuildPosts(tables);

        SetCache(posts);
        return _postsCache;
    }

    private void SetCache(List<Models.Post.Post>? posts)
    {
        if (posts is null)
        {
            _postsCache = null;
            return;
        }

        _postsCache = posts.ToDictionary(o => o.Id);
    }

    public async Task<List<Models.Post.Post>?> GetAll()
    {
        Dictionary<string, Models.Post.Post>? posts = await GetCache();
        return posts is null ? null : [.. posts.Values];
    }

    public async Task<int> GetCount() => (await GetCache())?.Count ?? 0;

    public async Task<int> MarkDeletedExcept(IReadOnlyCollection<string> keepPostIds)
    {
        await GetCache();

        if (_postsCache is null)
            return 0;

        HashSet<string> keep = keepPostIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        int deletedCount = 0;

        foreach (Models.Post.Post post in _postsCache.Values)
        {
            if (keep.Contains(post.Id) || post.Deleted)
                continue;

            post.Deleted = true;
            deletedCount++;
        }

        return deletedCount;
    }

    public async Task<List<Models.Post.MediaInput>?> GetMediaInputs()
    {
        PrepareTablesDirectories();
        string normalizedPostsPath = GetCurrentTablesFilePath(NormalizedPostsFileName);

        if (!File.Exists(normalizedPostsPath))
            return null;

        await Verify();

        LocalPostTables tables = await LoadTables();
        return BuildMediaInputs(tables);
    }

    public async Task<Dictionary<string, Models.Post.Post>?> GetAllAsDictionary() =>
        await GetCache();

    public async Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    )
    {
        if (profileIds.Count == 0)
            return [];

        HashSet<string> filter = profileIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (filter.Count == 0)
            return [];

        PrepareTablesDirectories();
        string normalizedPostsPath = GetCurrentTablesFilePath(NormalizedPostsFileName);

        if (!File.Exists(normalizedPostsPath))
            return [];

        await Verify();

        List<PostRow> posts = await ReadList<PostRow>(normalizedPostsPath);

        return posts
            .Where(post => filter.Contains(post.ProfileId))
            .GroupBy(post => post.ProfileId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private Task Verify()
    {
        if (!_config.Tasks.Verify)
            return Task.CompletedTask;

        string currentPath = GetCurrentTablesFilePath(NormalizedPostsFileName);

        if (!File.Exists(currentPath))
            return Task.CompletedTask;

        string basePath = GetPath(_partition.GetPrimary());

        var latestHistory = Directory
            .GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Date = Utils.Path.ToDate(path, isDir: true) })
            .Where(o => o.Date is not null)
            .OrderByDescending(o => o.Date)
            .FirstOrDefault();

        if (latestHistory is null)
            return Task.CompletedTask;

        string historyPath = Path.Combine(
            latestHistory.Path,
            Path.GetFileName(NormalizedPostsFileName)
        );

        if (!File.Exists(historyPath))
            return Task.CompletedTask;

        long currentLength = new FileInfo(currentPath).Length;
        long historyLength = new FileInfo(historyPath).Length;
        long diff = historyLength - currentLength;
        long threshold = Math.Max(0, _config.Tasks.VerifyMaxSizeDiffBytes);

        if (diff > threshold)
            throw new Exception(
                $"current '{NormalizedPostsFileName}' is smaller than latest history beyond threshold: current={currentLength}, history={historyLength}, shrink={diff}, threshold={threshold}, historyDir='{Path.GetFileName(latestHistory.Path)}'"
            );

        return Task.CompletedTask;
    }

    public async Task Save()
    {
        if (_postsCache is null)
            return;

        List<Models.Post.Post> posts = [.. _postsCache.Values];
        LocalPostTables tables = BuildTables(posts);

        await SaveTables(tables);
        Replicate();

        SetCache(posts);
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
        _logger.LogInformation("base path: {path}", Path.GetFileName(basePath));

        var pathsDate = Directory
            .GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
            .Select(o => new { Path = o, Date = Utils.Path.ToDate(o, isDir: true) })
            .Where(o => o.Date is not null)
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
            Directory.Delete(path, recursive: true);
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

        List<string> mainPaths = [.. GetDataFilePaths()];
        List<string> mainPathsFormatted = [.. mainPaths.Select(Utils.Path.GetPathFormatted)];

        foreach (Models.Config.Data.Partition partition in partitions)
        {
            List<string> paths = [.. GetDataFilePaths(partition)];
            List<string> pathsFormatted = [.. paths.Select(Utils.Path.GetPathFormatted)];

            for (int i = 0; i < paths.Count; i++)
            {
                string? pathDirectory = Path.GetDirectoryName(paths[i]);
                if (!string.IsNullOrWhiteSpace(pathDirectory))
                    Directory.CreateDirectory(pathDirectory);

                File.Delete(paths[i]);
                File.Copy(mainPaths[i], paths[i]);

                string? formattedPathDirectory = Path.GetDirectoryName(pathsFormatted[i]);

                if (!string.IsNullOrWhiteSpace(formattedPathDirectory))
                    Directory.CreateDirectory(formattedPathDirectory);

                File.Delete(pathsFormatted[i]);
                File.Copy(mainPathsFormatted[i], pathsFormatted[i]);
            }
        }
    }
}
