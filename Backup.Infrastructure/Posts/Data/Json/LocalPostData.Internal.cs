using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Data.Json;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Posts.Models;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Json;

public partial class LocalPostData
{
    private void SetupDirectory()
    {
        foreach (PartitionConfig partition in _partition.GetPartitions())
            Directory.CreateDirectory(GetPath(partition));
    }

    private string GetPath(PartitionConfig partition) =>
        UtilsPath.GetPath(
            [.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Post.Paths]
        );

    private async Task<Dictionary<string, Post>?> GetCache()
    {
        if (_postsCache is not null)
            return _postsCache;

        PrepareTablesDirectories();
        string normalizedPostsPath = GetCurrentTablesFilePath(NormalizedPostsFileName);

        if (!File.Exists(normalizedPostsPath))
            return null;

        await Verify();

        LocalPostTables tables = await LoadTables();
        List<Post> posts = BuildPosts(tables);

        SetCache(posts);
        return _postsCache;
    }

    private void SetCache(List<Post>? posts)
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
            .Select(path => new { Path = path, Date = UtilsPath.ToDate(path, isDir: true) })
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
        List<PartitionConfig> partitions = _partition
            .GetPartitions()
            .Except([_partition.GetPrimary()])
            .ToList();

        List<string> mainPaths = [.. GetDataFilePaths()];
        List<string> mainPathsFormatted = [.. mainPaths.Select(UtilsPath.GetPathFormatted)];

        foreach (PartitionConfig partition in partitions)
        {
            List<string> paths = [.. GetDataFilePaths(partition)];
            List<string> pathsFormatted = [.. paths.Select(UtilsPath.GetPathFormatted)];

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

    private Task PrunePartition(PartitionConfig partition)
    {
        _logger.LogInformation("prunning partition: {value}", partition.Id);

        string basePath = GetPath(partition);
        _logger.LogInformation("base path: {path}", Path.GetFileName(basePath));

        List<PostHistoryPath> pathsDate = Directory
            .GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Date = UtilsPath.ToDate(path, isDir: true) })
            .Where(entry => entry.Date is not null)
            .Select(entry => new PostHistoryPath(entry.Path, entry.Date!.Value))
            .ToList();

        _logger.LogInformation("paths: {value}", pathsDate.Count);

        if (pathsDate.Count == 0)
            return Task.CompletedTask;

        int keepDays = Math.Max(1, _appConfig.Tasks.Prune.Data.Post.KeepDays);
        int keepCount = Math.Max(0, _appConfig.Tasks.Prune.Data.Post.KeepCount);

        _logger.LogInformation(
            "prunning keep policy: keeping last {keepDays} stored days, found {kept} days",
            keepDays,
            pathsDate.Select(path => path.Date.Date).Distinct().Count()
        );

        List<string> remove =
        [
            .. _postHistoryPrunePolicyService.GetPathsToRemove(pathsDate, keepDays, keepCount),
        ];

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
