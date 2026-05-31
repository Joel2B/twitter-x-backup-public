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
        IReadOnlyList<PostHistoryPath> historyPaths = _postHistoryPathExtractionService.Extract(
            Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
        );
        PostSnapshotVerificationPlan plan = _postSnapshotVerificationPlanningService.Plan(
            Path.GetFileName(NormalizedPostsFileName),
            historyPaths
        );

        if (!plan.ShouldCompareWithHistory)
            return Task.CompletedTask;
        string historyPath = plan.HistoryFilePath;

        if (!File.Exists(historyPath))
            return Task.CompletedTask;

        long currentLength = new FileInfo(currentPath).Length;
        long historyLength = new FileInfo(historyPath).Length;
        _postSnapshotSizeGuardService.EnsureNotShrunkBeyondThreshold(
            currentLength,
            historyLength,
            _config.Tasks.VerifyMaxSizeDiffBytes,
            NormalizedPostsFileName,
            plan.HistoryDirectoryName
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
            IReadOnlyList<PostDataFileReplicationOperation> operations =
                _postDataReplicationPlanningService.Plan(mainPaths, paths);
            IReadOnlyList<PostDataFileReplicationOperation> formattedOperations =
                _postDataReplicationPlanningService.Plan(mainPathsFormatted, pathsFormatted);

            foreach (PostDataFileReplicationOperation operation in operations)
            {
                string? pathDirectory = Path.GetDirectoryName(operation.TargetPath);

                if (!string.IsNullOrWhiteSpace(pathDirectory))
                    Directory.CreateDirectory(pathDirectory);

                if (File.Exists(operation.TargetPath))
                    File.Delete(operation.TargetPath);

                File.Copy(operation.SourcePath, operation.TargetPath);
            }

            foreach (PostDataFileReplicationOperation operation in formattedOperations)
            {
                string? formattedPathDirectory = Path.GetDirectoryName(operation.TargetPath);

                if (!string.IsNullOrWhiteSpace(formattedPathDirectory))
                    Directory.CreateDirectory(formattedPathDirectory);

                if (File.Exists(operation.TargetPath))
                    File.Delete(operation.TargetPath);

                File.Copy(operation.SourcePath, operation.TargetPath);
            }
        }
    }

    private Task PrunePartition(PartitionConfig partition)
    {
        _logger.LogInformation("prunning partition: {value}", partition.Id);

        string basePath = GetPath(partition);
        _logger.LogInformation("base path: {path}", Path.GetFileName(basePath));

        IReadOnlyList<PostHistoryPath> pathsDate = _postHistoryPathExtractionService.Extract(
            Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
        );

        _logger.LogInformation("paths: {value}", pathsDate.Count);

        if (pathsDate.Count == 0)
            return Task.CompletedTask;

        var plan = _postHistoryPrunePlanningService.Plan(
            pathsDate,
            _appConfig.Tasks.Prune.Data.Post.KeepDays,
            _appConfig.Tasks.Prune.Data.Post.KeepCount
        );

        _logger.LogInformation(
            "prunning keep policy: keeping last {keepDays} stored days, found {kept} days",
            plan.NormalizedKeepDays,
            plan.DistinctDayCount
        );

        List<string> remove = [.. plan.PathsToRemove];

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
