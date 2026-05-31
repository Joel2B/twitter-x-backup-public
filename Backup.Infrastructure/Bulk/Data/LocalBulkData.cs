using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Application.IO;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Bulk.Models;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Bulk;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Bulk.Data;

public class LocalBulkData(
    ILogger<LocalBulkData> _logger,
    AppConfig _appConfig,
    StorageBulk _config,
    IPartition _partition,
    IBulkPrunePolicyService bulkPrunePolicyService,
    IBulkArchiveFilePolicyService bulkArchiveFilePolicyService,
    IDataStoreGuardService dataStoreGuardService
) : IBulkDataStore, ISetup
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<LocalBulkData> _logger = _logger;
    private readonly AppConfig _appConfig = _appConfig;
    private readonly StorageBulk _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IBulkPrunePolicyService _bulkPrunePolicyService = bulkPrunePolicyService;
    private readonly IBulkArchiveFilePolicyService _bulkArchiveFilePolicyService =
        bulkArchiveFilePolicyService;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;

    public Task Setup()
    {
        SetupDirectory();

        return Task.CompletedTask;
    }

    private string GetPath(PartitionConfig partition) =>
        UtilsPath.GetPath(
            [.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Bulk.Paths]
        );

    private string GetFileBulk(PartitionConfig? partition = null)
    {
        string fileName = _dataStoreGuardService.RequireConfiguredFileName(_config.Paths.Bulk.File);

        PartitionConfig primary = partition ?? _partition.GetPrimary();
        string path = Path.Combine(GetPath(primary), fileName);

        return path;
    }

    private void SetupDirectory()
    {
        foreach (PartitionConfig partition in _partition.GetPartitions())
            Directory.CreateDirectory(GetPath(partition));
    }

    public async Task<List<BulkData>?> GetBulks()
    {
        string path = GetFileBulk();

        if (!File.Exists(path))
            return null;

        string content = await File.ReadAllTextAsync(path);

        List<BulkData>? deserialized = JsonConvert.DeserializeObject<List<BulkData>>(content);
        List<BulkData> bulks = _dataStoreGuardService.RequireDeserialized(
            deserialized,
            "Error deserializing the file."
        );

        return bulks;
    }

    public async Task Save(List<BulkData> bulks)
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

        string newPath = _bulkArchiveFilePolicyService.BuildArchivePath(path, DateTime.Now);

        await Task.Delay(1000);
        File.Move(path, newPath);
    }

    public async Task Prune()
    {
        _logger.LogInformation("running prune");
        _logger.LogInformation("prune: {value}", _config.Tasks.Prune);

        if (!_config.Tasks.Prune)
            return;

        foreach (PartitionConfig partition in _partition.GetPartitions())
            await PrunePartition(partition);
    }

    private Task PrunePartition(PartitionConfig partition)
    {
        _logger.LogInformation("prunning partition: {value}", partition.Id);

        string basePath = GetPath(partition);
        string[] pathsFiles = Directory.GetFiles(basePath, "*.json", SearchOption.TopDirectoryOnly);
        _logger.LogInformation("base path: {path}", Path.GetFileName(basePath));

        List<DatedPath> datedPaths = pathsFiles
            .Select(path => new { Path = path, Date = UtilsPath.ToDate(path) })
            .Where(entry => entry.Date is not null)
            .Select(entry => new DatedPath(entry.Path, entry.Date!.Value))
            .OrderBy(entry => entry.Date)
            .ToList();

        if (datedPaths.Count == 0)
            return Task.CompletedTask;

        DateTime date = datedPaths.Last().Date.AddDays(-_appConfig.Tasks.Prune.Data.Post.KeepDays);

        _logger.LogInformation(
            "prunning date: {date}, KeepDays: {keepDays}",
            date,
            _appConfig.Tasks.Prune.Data.Post.KeepDays
        );

        List<string> pathsToRemove =
        [
            .. _bulkPrunePolicyService.GetPathsToRemove(
                datedPaths,
                _appConfig.Tasks.Prune.Data.Post.KeepDays
            ),
        ];

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
        List<PartitionConfig> partitions = _partition
            .GetPartitions()
            .Except([_partition.GetPrimary()])
            .ToList();

        string mainPath = GetFileBulk();

        foreach (PartitionConfig partition in partitions)
        {
            string path = GetFileBulk(partition);

            File.Delete(path);
            File.Copy(mainPath, path);
        }
    }
}
