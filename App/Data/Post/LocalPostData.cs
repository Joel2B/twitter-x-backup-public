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
    private Dictionary<string, PostMetaRow>? _postMetaCache = null;

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
            _postMetaCache = null;
            return;
        }

        _postsCache = posts.ToDictionary(o => o.Id);
    }

    public async Task<List<Models.Post.Post>?> GetAll()
    {
        Dictionary<string, Models.Post.Post>? posts = await GetCache();
        return posts is null ? null : [.. posts.Values];
    }

    public async Task<Dictionary<string, string>> GetHashesById()
    {
        Dictionary<string, PostMetaRow> postMeta = await GetPostMetaCache();

        return postMeta.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Hash,
            StringComparer.Ordinal
        );
    }

    public async Task<List<Models.Post.Post>> GetByIds(IReadOnlyCollection<string> ids)
    {
        if (ids.Count == 0)
            return [];

        HashSet<string> filter = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (filter.Count == 0)
            return [];

        Dictionary<string, Models.Post.Post>? posts = await GetCache();

        if (posts is null)
            return [];

        List<Models.Post.Post> result = new(filter.Count);

        foreach (string id in filter)
        {
            if (!posts.TryGetValue(id, out Models.Post.Post? post))
                continue;

            result.Add(post.Clone());
        }

        return result;
    }

    public async Task<int> GetCount() => (await GetCache())?.Count ?? 0;

    public async Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    )
    {
        await GetCache();

        if (_postsCache is null)
            return 0;

        HashSet<string> keep = keepPostIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        List<Models.Post.Post> deletedPosts = [];

        foreach (Models.Post.Post post in _postsCache.Values)
        {
            bool hasScope =
                post.Index.TryGetValue(userId, out Dictionary<string, Models.Post.IndexData>? index)
                && index.ContainsKey(origin);

            if (!hasScope)
                continue;

            if (keep.Contains(post.Id) || post.Deleted)
                continue;

            Models.Post.Post deletedPost = post.Clone();
            deletedPost.Deleted = true;
            deletedPosts.Add(deletedPost);
        }

        if (deletedPosts.Count == 0)
            return 0;

        await AddPosts(userId, origin, deletedPosts, new() { Index = false });
        return deletedPosts.Count;
    }

    public async Task<List<Models.Post.MediaInput>?> GetMediaInputs()
    {
        Dictionary<string, Models.Post.Post>? posts = await GetCache();
        if (posts is null)
            return null;

        List<Models.Post.MediaInput> current = [.. posts.Values.Select(ToMediaInput)];

        List<Models.Post.MediaInput> history = posts
            .Values.SelectMany(post => post.Changes)
            .Where(change => change.Data is not null)
            .Select(change => ToMediaInput(change.Data!))
            .ToList();

        current.AddRange(history);
        return current;
    }

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

        static Dictionary<string, int> CountByProfileIds(
            IEnumerable<string> profileIds,
            HashSet<string> filter
        ) =>
            profileIds
                .Where(filter.Contains)
                .GroupBy(profileId => profileId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Dictionary<string, Models.Post.Post>? cache = await GetCache();
        if (cache is null)
            return [];

        return CountByProfileIds(cache.Values.Select(post => post.Profile.Id), filter);
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
        Dictionary<string, PostMetaRow> postMeta = await EnsurePostMetaCache(posts);

        await SaveTables(tables, postMeta);
        Replicate();

        SetCache(posts);
        _postMetaCache = postMeta;
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
            .Select(o => new
            {
                Date = o.Key,
                Paths = o.OrderBy(o => o.Date).Select(o => o.Path).ToList(),
            })
            .OrderBy(o => o.Date)
            .ToList();

        _logger.LogInformation("paths: {value}", pathsDate.Count);

        if (pathsDate.Count == 0)
            return Task.CompletedTask;

        int keepDays = Math.Max(1, _appConfig.Tasks.Prune.Data.Post.KeepDays);
        int keepCount = Math.Max(0, _appConfig.Tasks.Prune.Data.Post.KeepCount);

        HashSet<DateTime> keepDates =
        [
            .. pathsDate.OrderByDescending(o => o.Date).Take(keepDays).Select(o => o.Date),
        ];

        _logger.LogInformation(
            "prunning keep policy: keeping last {keepDays} stored days, found {kept} days",
            keepDays,
            keepDates.Count
        );

        List<string> remove = [];

        foreach (var day in pathsDate)
        {
            if (!keepDates.Contains(day.Date))
            {
                remove.AddRange(day.Paths);
                continue;
            }

            int removeCount = Math.Max(0, day.Paths.Count - keepCount);
            remove.AddRange(day.Paths.Take(removeCount));
        }

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
