using System.Collections.Concurrent;
using Backup.Application.Partition;
using Backup.Application.Partition.Models;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Partition;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Data.Partition;

public class LocalPartition(
    AppConfig _appConfig,
    ILogger<LocalPartition> _logger,
    IPartitionResolutionService partitionResolutionService,
    IPartitionPathProbeExecutionService partitionPathProbeExecutionService,
    Storage? _config = null
) : IPartition, ISetup
{
    private readonly ILogger<LocalPartition> _logger = _logger;
    private readonly Storage? _config = _config;
    private readonly AppConfig _appConfig = _appConfig;
    private readonly IPartitionResolutionService _partitionResolutionService =
        partitionResolutionService;
    private readonly IPartitionPathProbeExecutionService _partitionPathProbeExecutionService =
        partitionPathProbeExecutionService;

    private readonly ConcurrentDictionary<int, PartitionSize> _partitions = new(
        _appConfig
            .Data.Partitions.ToDictionary(o => o.Id)
            .ToDictionary(o => o.Key, o => new PartitionSize { Size = 0, Partition = o.Value })
    );

    public Task Setup()
    {
        PrintAliases();
        Print();
        SetupPaths();
        Print();
        Check();

        return Task.CompletedTask;
    }

    private void PrintAliases()
    {
        _logger.LogInformation("{alias,-5} {value,-5}", "Alias", "Value");

        foreach (var kvp in _appConfig.Data.Aliases)
            _logger.LogInformation("{alias,-5} {value,-5}", kvp.Key, kvp.Value);
    }

    private void SetupPaths()
    {
        foreach (PartitionConfig partition in _appConfig.Data.Partitions)
            partition.Paths = [UtilsPath.GetPartitionPath(_appConfig, partition)];
    }

    private void Print()
    {
        _logger.LogInformation(
            "{id,-5} {enabled,-5} {partition,-9} {size,-10} {path}",
            "Id",
            "Enabled",
            "Partition",
            "Size",
            "Path"
        );

        foreach (var kvp in _partitions)
        {
            _logger.LogInformation(
                "{id,-5} {enabled,-5} {partition,-9} {size,-10} {path}",
                kvp.Value.Partition.Id,
                kvp.Value.Partition.Enabled,
                kvp.Value.Partition.Name,
                UtilsStorage.FormatBytes(kvp.Value.Size),
                Path.Combine([.. kvp.Value.Partition.Paths])
            );
        }
    }

    private void Check()
    {
        _logger.LogInformation("{title}", "Testing paths");
        _logger.LogInformation("{partition,-9} {error}", "Partition", "Error");

        PartitionPathProbeExecutionResult probe = _partitionPathProbeExecutionService.Execute(
            _partitions.Values.Select(item => new PartitionPathProbeCandidate
            {
                PartitionName = item.Partition.Name ?? item.Partition.Id.ToString(),
                Enabled = item.Partition.Enabled,
                RootPath = Path.Combine([.. item.Partition.Paths]),
            })
        );

        foreach (PartitionPathProbeResult target in probe.Results)
        {
            _logger.LogInformation("{partition,-9} {error}", target.PartitionName, target.Error);
        }

        if (probe.HasErrors)
            throw new IOException();
    }

    public void SetupSizes(Dictionary<int, long> sizes)
    {
        foreach (var kvp in sizes)
        {
            if (!_partitions.ContainsKey(kvp.Key))
                continue;

            _partitions[kvp.Key].Size = kvp.Value;
        }

        Print();
    }

    public List<PartitionConfig> GetPartitions(List<int>? ids = null)
    {
        List<int>? selectedIds = ids ?? _config?.Partitions;
        IReadOnlyCollection<int> filteredIds = _partitionResolutionService.SelectEnabledIds(
            BuildPartitionStateSources(_appConfig.Data.Partitions),
            selectedIds
        );
        return [.. _appConfig.Data.Partitions.Where(partition => filteredIds.Contains(partition.Id))];
    }

    public PartitionConfig GetPath(int? id = null, long size = 0)
    {
        List<PartitionConfig> available = GetPartitions();
        int selectedId = _partitionResolutionService.ResolvePartitionId(
            BuildPartitionStateSources(available),
            id,
            size
        );

        if (!_partitions.TryGetValue(selectedId, out PartitionSize? partition))
            throw new InvalidOperationException($"not exist partition id {selectedId}");

        if (id is null && size > 0)
            partition.Add(size);

        return partition.Partition;
    }

    public List<PartitionConfig> GetCache()
    {
        List<PartitionConfig> available = GetPartitions();
        IReadOnlyCollection<int> cacheIds = _partitionResolutionService.SelectCacheIds(
            BuildPartitionStateSources(available)
        );
        return [.. available.Where(partition => cacheIds.Contains(partition.Id))];
    }

    public PartitionConfig GetPrimary()
    {
        List<PartitionConfig> available = GetPartitions();
        int id = _partitionResolutionService.GetRequiredPartitionIdByType(
            BuildPartitionStateSources(available),
            type: "primary"
        );
        return _partitions[id].Partition;
    }

    public PartitionConfig GetHeavy()
    {
        List<PartitionConfig> available = GetPartitions();
        int id = _partitionResolutionService.GetRequiredPartitionIdByType(
            BuildPartitionStateSources(available),
            type: "heavy"
        );
        return _partitions[id].Partition;
    }

    private IEnumerable<PartitionStateSource> BuildPartitionStateSources(
        IReadOnlyList<PartitionConfig> partitions
    ) =>
        partitions.Select(partition =>
        {
            _partitions.TryGetValue(partition.Id, out PartitionSize? current);
            return new PartitionStateSource
            {
                Id = partition.Id,
                Type = partition.Type,
                Tags = partition.Tags,
                Size = partition.Size,
                UsableSpace = partition.UsableSpace,
                Enabled = partition.Enabled,
                CurrentSize = current?.Size ?? 0,
            };
        });
}
