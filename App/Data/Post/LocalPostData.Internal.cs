using Backup.App.Models.Data.Json;
using Microsoft.Extensions.Logging;

namespace Backup.App.Data.Post;

public partial class LocalPostData
{
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

    private void Replicate()
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
}
