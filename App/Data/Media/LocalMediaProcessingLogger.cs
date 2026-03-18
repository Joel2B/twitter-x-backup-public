using Backup.App.Interfaces.Partition;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Data.Media;

public class LocalMediaProcessingLogger(
    ILogger<LocalMediaProcessingLogger> _logger,
    Models.Config.App _config,
    IPartition _partition
) : IMediaProcessingLogger
{
    private readonly ILogger<LocalMediaProcessingLogger> _logger = _logger;
    public readonly Models.Config.App _config = _config;
    private readonly IPartition _partition = _partition;

    private readonly string date = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");

    private void SetupDirectory()
    {
        Directory.CreateDirectory(GetPathDate());
    }

    public string GetPath()
    {
        Models.Config.Data.Partition partition = _partition
            .GetPartitions(_config.Debug.Partitions)
            .First();

        return Path.Combine(
            [
                .. partition.Paths,
                .. _config.Debug.Paths,
                .. _config.Debug.Media.Paths,
                .. _config.Debug.Media.Url.Paths,
            ]
        );
    }

    private string GetPathDate() => Path.Combine([GetPath(), date]);

    public async Task Save(string type, List<Download> downloads)
    {
        SetupDirectory();

        string fileName = $"{type}.json";
        string path = Path.Combine(GetPathDate(), fileName);

        await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(downloads));
    }

    public Task Prune()
    {
        if (!_config.Debug.Media.Url.Prune.Enabled)
            return Task.CompletedTask;

        string basePath = GetPath();
        string[] paths = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly);

        var pathsDate = paths
            .Where(o => Utils.Path.ToDate(o, true) is not null)
            .Select(o => new { Path = o, Date = Utils.Path.ToDate(o, true) });

        DateTime? date = pathsDate
            .SkipLast(_config.Debug.Media.Url.Prune.RetainedCountLimit)
            .LastOrDefault()
            ?.Date;

        if (date is null)
            return Task.CompletedTask;

        List<string> pathsToRemove = pathsDate
            .Where(o => o.Date <= date)
            .Select(o => o.Path)
            .ToList();

        if (pathsToRemove.Count == 0)
            return Task.CompletedTask;

        _logger.LogInformation("base path: {path}", Path.GetFileName(basePath));

        foreach (string path in pathsToRemove)
        {
            Directory.Delete(path, true);
            _logger.LogInformation("{path} removed", Path.GetFileName(path));
        }

        return Task.CompletedTask;
    }
}
