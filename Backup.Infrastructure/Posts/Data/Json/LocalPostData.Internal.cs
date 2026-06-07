using Backup.Application.Posts.Models;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Models.Stored;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Json;

public partial class LocalPostData
{
    private void SetupDirectory()
    {
        _logger.LogInformation("setup-directory: creating partition directories");

        foreach (PartitionConfig partition in _partition.GetPartitions())
        {
            _logger.LogInformation(
                "setup-directory: ensuring partition {partitionId}",
                partition.Id
            );

            Directory.CreateDirectory(GetPath(partition));
        }
    }

    private string GetPath(PartitionConfig partition) =>
        UtilsPath.GetPath(
            [.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Post.Paths]
        );

    private async Task<Dictionary<string, Post>?> GetCache()
    {
        if (_postsCache is not null)
        {
            _logger.LogInformation(
                "get-cache: using in-memory cache with {count} posts",
                _postsCache.Count
            );
            return _postsCache;
        }

        _logger.LogInformation("get-cache: preparing tables directories");
        PrepareTablesDirectories();
        string normalizedPostsPath = GetCurrentTablesFilePath(NormalizedPostsFileName);

        if (!File.Exists(normalizedPostsPath))
        {
            _logger.LogInformation("get-cache: no normalized posts file found");
            return null;
        }

        _logger.LogInformation("get-cache: verifying snapshot");
        await Verify();

        _logger.LogInformation("get-cache: loading tables");
        LocalPostTables tables = await LoadTables();

        _logger.LogInformation("get-cache: building posts from tables");
        List<Post> posts = BuildPosts(tables);

        SetCache(posts);
        _logger.LogInformation("get-cache: completed with {count} posts", _postsCache?.Count ?? 0);

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
        string currentPath = GetCurrentTablesFilePath(NormalizedPostsFileName);
        bool currentExists = File.Exists(currentPath);

        string basePath = GetPath(_partition.GetPrimary());
        IReadOnlyList<PostHistoryPath> historyPaths = _historyCoordinator.ExtractHistoryPaths(
            Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
        );

        PostSnapshotVerificationDecision decision = _historyCoordinator.BuildSnapshotDecision(
            _config.Tasks.Verify,
            currentExists,
            Path.GetFileName(NormalizedPostsFileName),
            historyPaths
        );

        if (!decision.ShouldInspectHistoryFile)
        {
            _logger.LogInformation("verify: skipping history inspection");
            return Task.CompletedTask;
        }

        string historyPath = decision.HistoryFilePath;

        _logger.LogInformation(
            "verify: inspecting history snapshot {historyFile}",
            Path.GetFileName(historyPath)
        );

        bool historyExists = File.Exists(historyPath);

        if (!historyExists)
        {
            _logger.LogInformation("verify: history snapshot missing, skipping");
            return Task.CompletedTask;
        }

        long currentLength = new FileInfo(currentPath).Length;
        long historyLength = new FileInfo(historyPath).Length;

        _historyCoordinator.ValidateSnapshotIfNeeded(
            decision,
            historyExists,
            currentLength,
            historyLength,
            _config.Tasks.VerifyMaxSizeDiffBytes
        );

        _logger.LogInformation(
            "verify: completed, currentBytes={currentLength}, historyBytes={historyLength}",
            currentLength,
            historyLength
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

        _logger.LogInformation(
            "replicate: starting for {partitionCount} secondary partitions",
            partitions.Count
        );

        foreach (PartitionConfig partition in partitions)
        {
            List<string> paths = [.. GetDataFilePaths(partition)];

            IReadOnlyList<PostDataFileReplicationOperation> operations =
                _historyCoordinator.PlanReplication(mainPaths, paths);

            _logger.LogInformation(
                "replicate: partition {partitionId}, files={operationCount}",
                partition.Id,
                operations.Count
            );

            ApplyReplicationOperations(operations);
        }

        _logger.LogInformation("replicate: completed");
    }

    private static void ApplyReplicationOperations(
        IReadOnlyList<PostDataFileReplicationOperation> operations
    )
    {
        foreach (PostDataFileReplicationOperation operation in operations)
        {
            string? directory = Path.GetDirectoryName(operation.TargetPath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(operation.TargetPath))
                File.Delete(operation.TargetPath);

            File.Copy(operation.SourcePath, operation.TargetPath);
        }
    }

    private Task PrunePartition(PartitionConfig partition)
    {
        _logger.LogInformation("prunning partition: {value}", partition.Id);

        string basePath = GetPath(partition);
        _logger.LogInformation("base path: {path}", Path.GetFileName(basePath));

        IReadOnlyList<PostHistoryPath> pathsDate = _historyCoordinator.ExtractHistoryPaths(
            Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
        );

        _logger.LogInformation("paths: {value}", pathsDate.Count);

        if (pathsDate.Count == 0)
            return Task.CompletedTask;

        var plan = _historyCoordinator.PlanPrune(
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
