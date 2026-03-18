using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Bulk;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Bulk;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Data.Bulk;

public class LocalBulkData(
    ILogger<LocalBulkData> _logger,
    Models.Config.App _appConfig,
    Storage _config,
    IPartition _partition
) : IBulkData, ISetup
{
    public string? Id { get; set; }

    private readonly ILogger<LocalBulkData> _logger = _logger;
    private readonly Models.Config.App _appConfig = _appConfig;
    private readonly Storage _config = _config;
    private readonly IPartition _partition = _partition;

    public Task Setup()
    {
        SetupDirectory();

        return Task.CompletedTask;
    }

    private string GetPath(Models.Config.Data.Partition partition) =>
        Utils.Path.GetPath(
            [.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Bulk.Paths]
        );

    private string GetFileBulk(Models.Config.Data.Partition? partition = null)
    {
        if (_config.Paths.Bulk.File is null)
            throw new Exception("file not configured");

        Models.Config.Data.Partition primary = partition ?? _partition.GetPrimary();
        string path = Path.Combine(GetPath(primary), _config.Paths.Bulk.File);

        return path;
    }

    private void SetupDirectory()
    {
        foreach (Models.Config.Data.Partition partition in _partition.GetPartitions())
            Directory.CreateDirectory(GetPath(partition));
    }

    public async Task<List<Models.Bulk.Bulk>?> GetBulks()
    {
        string path = GetFileBulk();

        if (!File.Exists(path))
            return null;

        string content = await File.ReadAllTextAsync(path);

        List<Models.Bulk.Bulk>? bulks =
            JsonConvert.DeserializeObject<List<Models.Bulk.Bulk>>(content)
            ?? throw new Exception("Error deserializing the file.");

        return bulks;
    }

    public async Task Save(List<Models.Bulk.Bulk> bulks)
    {
        await RenameFile();

        string path = GetFileBulk();
        string data = JsonConvert.SerializeObject(bulks, Formatting.Indented);

        await File.WriteAllTextAsync(path, data);
        Replicate();
    }

    private async Task RenameFile()
    {
        string path = GetFileBulk();

        if (!File.Exists(path))
            return;

        string date = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");
        string fileName = $"{date}.json";
        string newPath = path.Replace(Path.GetFileName(path), fileName);

        await Task.Delay(1000);
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
            .Select(o => new { Path = o, Date = Utils.Path.ToDate(o) });

        if (!pathsDate.Any())
            return Task.CompletedTask;

        DateTime? date = pathsDate.Last().Date?.AddDays(-_appConfig.Tasks.Prune.Data.Post.KeepDays);

        _logger.LogInformation(
            "prunning date: {date}, KeepDays: {keepDays}",
            date,
            _appConfig.Tasks.Prune.Data.Post.KeepDays
        );

        if (date is null)
            return Task.CompletedTask;

        List<string> pathsToRemove = pathsDate
            .Where(o => o.Date < date)
            .Select(o => o.Path)
            .ToList();

        _logger.LogInformation("prunning {value} paths", pathsToRemove.Count);

        if (pathsToRemove.Count == 0)
            return Task.CompletedTask;

        foreach (string path in pathsToRemove)
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

        string mainPath = GetFileBulk();

        foreach (Models.Config.Data.Partition partition in partitions)
        {
            string path = GetFileBulk(partition);

            File.Delete(path);
            File.Copy(mainPath, path);
        }
    }
}
