using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Application.Core;
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
    ISecondaryStoreSelectionService secondaryStoreSelectionService,
    IBulkDatedPathExtractionService bulkDatedPathExtractionService,
    IBulkPruneExecutionService bulkPruneExecutionService,
    IBulkReplicationPathPlanningService bulkReplicationPathPlanningService,
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
    private readonly ISecondaryStoreSelectionService _secondaryStoreSelectionService =
        secondaryStoreSelectionService;
    private readonly IBulkDatedPathExtractionService _bulkDatedPathExtractionService =
        bulkDatedPathExtractionService;
    private readonly IBulkPruneExecutionService _bulkPruneExecutionService = bulkPruneExecutionService;
    private readonly IBulkReplicationPathPlanningService _bulkReplicationPathPlanningService =
        bulkReplicationPathPlanningService;
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

        List<PartitionConfig> partitions = _partition.GetPartitions();
        Dictionary<int, PartitionConfig> partitionsById = partitions.ToDictionary(partition => partition.Id);
        List<BulkPrunePartitionExecutionInput> inputs = [];

        foreach (PartitionConfig partition in partitions)
        {
            string basePath = GetPath(partition);
            string[] pathsFiles = Directory.GetFiles(basePath, "*.json", SearchOption.TopDirectoryOnly);
            List<DatedPath> datedPaths = _bulkDatedPathExtractionService
                .Extract(pathsFiles)
                .OrderBy(entry => entry.Date)
                .ToList();

            inputs.Add(
                new BulkPrunePartitionExecutionInput
                {
                    PartitionId = partition.Id,
                    DatedPaths = datedPaths,
                }
            );
        }

        IReadOnlyList<BulkPrunePartitionExecutionPlan> plans = _bulkPruneExecutionService.PlanPartitions(
            inputs,
            _config.Tasks.Prune,
            _appConfig.Tasks.Prune.Data.Post.KeepDays
        );

        foreach (BulkPrunePartitionExecutionPlan plan in plans)
        {
            if (!partitionsById.TryGetValue(plan.PartitionId, out PartitionConfig? partition))
                continue;

            string basePath = GetPath(partition);
            _logger.LogInformation("prunning partition: {value}", partition.Id);
            _logger.LogInformation("base path: {path}", Path.GetFileName(basePath));
            _logger.LogInformation(
                "prunning date: {date}, KeepDays: {keepDays}",
                plan.ThresholdDate,
                _appConfig.Tasks.Prune.Data.Post.KeepDays
            );
            _logger.LogInformation("prunning {value} paths", plan.PathsToRemove.Count);

            foreach (string path in plan.PathsToRemove)
            {
                File.Delete(path);
                _logger.LogInformation("{path} removed", Path.GetFileName(path));
            }
        }

        await Task.CompletedTask;
    }

    public void Replicate()
    {
        PartitionConfig primary = _partition.GetPrimary();
        IReadOnlyList<PartitionConfig> partitions = _secondaryStoreSelectionService.SelectSecondaries(
            _partition.GetPartitions(),
            primary
        );

        string mainPath = GetFileBulk();
        IReadOnlyList<string> replicaPaths = _bulkReplicationPathPlanningService.GetReplicaPaths(
            mainPath,
            partitions.Select(GetFileBulk)
        );

        foreach (string path in replicaPaths)
        {
            if (File.Exists(path))
                File.Delete(path);
            File.Copy(mainPath, path);
        }
    }
}
