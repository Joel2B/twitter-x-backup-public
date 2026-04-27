using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Partition;
using Microsoft.Extensions.Logging;

namespace Backup.App.Data.Post;

public class LocalPostLogger(
    ILogger<LocalPostLogger> _logger,
    Models.Config.App _config,
    IPartition _partition
) : IPostLogger
{
    private readonly ILogger<LocalPostLogger> _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly IPartition _partition = _partition;

    private string _id = "";
    private string _path = "";
    private int _index = 0;

    private void SetupDirectory(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            sourceId = "unknown";

        if (_id != "" && _id == sourceId)
            return;

        _id = sourceId;

        string date = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");

        _path = Path.Combine([GetPath(), _id, date]);
        Directory.CreateDirectory(_path);

        _index = 0;
    }

    private string GetPath()
    {
        Models.Config.Data.Partition partition = _partition
            .GetPartitions(_config.Debug.Partitions)
            .First();

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

            var pathsDate = subPaths
                .Where(o => Utils.Path.ToDate(o, true) is not null)
                .Select(o => new { Path = o, Date = Utils.Path.ToDate(o, true) })
                .OrderBy(o => o.Date)
                .ToList();

            DateTime? date = pathsDate
                .SkipLast(_config.Debug.Api.Prune.RetainedCountLimit)
                .LastOrDefault()
                ?.Date;

            if (date is null)
                continue;

            List<string> pathsToRemove = pathsDate
                .Where(o => o.Date <= date)
                .Select(o => o.Path)
                .ToList();

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
