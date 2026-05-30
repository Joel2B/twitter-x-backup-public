using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Json;

public class LocalPostLogger(
    ILogger<LocalPostLogger> _logger,
    AppConfig _config,
    IPartition _partition,
    IPostDebugLogPrunePolicyService postDebugLogPrunePolicyService,
    IPostLogFolderPolicyService postLogFolderPolicyService
) : IPostLogger
{
    private readonly ILogger<LocalPostLogger> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IPostDebugLogPrunePolicyService _postDebugLogPrunePolicyService =
        postDebugLogPrunePolicyService;
    private readonly IPostLogFolderPolicyService _postLogFolderPolicyService =
        postLogFolderPolicyService;

    private string _id = "";
    private string _path = "";
    private int _index = 0;

    private void SetupDirectory(string sourceId)
    {
        if (_id != "" && _id == sourceId)
            return;

        _id = sourceId;

        string date = _postLogFolderPolicyService.CreateSessionFolderName(DateTime.Now);

        _path = Path.Combine([GetPath(), _id, date]);
        Directory.CreateDirectory(_path);

        _index = 0;
    }

    private string GetPath()
    {
        PartitionConfig partition = _partition.GetPartitions(_config.Debug.Partitions).First();

        return Path.Combine(
            [.. partition.Paths, .. _config.Debug.Paths, .. _config.Debug.Api.Paths]
        );
    }

    public async Task Save(string sourceId, string data, CancellationToken token)
    {
        SetupDirectory(sourceId);

        string fileName = $"{_index}.json";
        string path = Path.Combine(_path, fileName);

        await File.WriteAllTextAsync(path, data, token);

        _index++;
    }

    public Task Prune()
    {
        if (!_config.Debug.Api.Prune.Enabled)
            return Task.CompletedTask;

        string[] paths = Directory.GetDirectories(GetPath(), "*", SearchOption.TopDirectoryOnly);

        foreach (string path in paths)
        {
            string[] subPaths = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);

            List<PostHistoryPath> pathsDate = subPaths
                .Select(path => new { Path = path, Date = UtilsPath.ToDate(path, true) })
                .Where(entry => entry.Date is not null)
                .Select(entry => new PostHistoryPath(entry.Path, entry.Date!.Value))
                .ToList();

            List<string> pathsToRemove =
            [
                .. _postDebugLogPrunePolicyService.GetPathsToRemove(
                    pathsDate,
                    _config.Debug.Api.Prune.RetainedCountLimit
                ),
            ];

            if (pathsToRemove.Count == 0)
                continue;

            _logger.LogInformation("base path: {path}", Path.GetFileName(path));

            foreach (string subPath in pathsToRemove)
            {
                Directory.Delete(subPath, true);
                _logger.LogInformation("{path} removed", Path.GetFileName(subPath));
            }
        }

        return Task.CompletedTask;
    }
}
