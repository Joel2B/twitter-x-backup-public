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
    IPartitionPolicyService partitionPolicyService,
    IPartitionPathProbeService partitionPathProbeService,
    IPartitionPathProbePlanningService partitionPathProbePlanningService,
    Storage? _config = null
) : IPartition, ISetup
{
    private readonly ILogger<LocalPartition> _logger = _logger;
    private readonly Storage? _config = _config;
    private readonly AppConfig _appConfig = _appConfig;
    private readonly IPartitionPolicyService _partitionPolicyService = partitionPolicyService;
    private readonly IPartitionPathProbeService _partitionPathProbeService = partitionPathProbeService;
    private readonly IPartitionPathProbePlanningService _partitionPathProbePlanningService =
        partitionPathProbePlanningService;

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

        bool stop = false;
        IReadOnlyList<PartitionPathProbeTarget> targets =
            _partitionPathProbePlanningService.BuildTargets(
                _partitions.Values.Select(item => new PartitionPathProbeCandidate
                {
                    PartitionName = item.Partition.Name ?? item.Partition.Id.ToString(),
                    Enabled = item.Partition.Enabled,
                    RootPath = Path.Combine([.. item.Partition.Paths]),
                })
            );

        foreach (PartitionPathProbeTarget target in targets)
        {
            string? error = _partitionPathProbeService.Probe(target.ProbePath);
            _logger.LogInformation("{partition,-9} {error}", target.PartitionName, error);

            if (error is not null)
                stop = true;
        }

        if (stop)
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
        List<PartitionState> states = BuildPartitionStates(_appConfig.Data.Partitions);
        IReadOnlyList<PartitionState> filtered = _partitionPolicyService.FilterEnabled(
            states,
            selectedIds
        );
        HashSet<int> filteredIds = [.. filtered.Select(state => state.Id)];
        return [.. _appConfig.Data.Partitions.Where(partition => filteredIds.Contains(partition.Id))];
    }

    public PartitionConfig GetPath(int? id = null, long size = 0)
    {
        List<PartitionConfig> available = GetPartitions();
        List<PartitionState> states = BuildPartitionStates(available);
        int selectedId = _partitionPolicyService.ResolvePartitionId(states, id, size);

        if (!_partitions.TryGetValue(selectedId, out PartitionSize? partition))
            throw new InvalidOperationException($"not exist partition id {selectedId}");

        if (id is null && size > 0)
            partition.Add(size);

        return partition.Partition;
    }

    public List<PartitionConfig> GetCache() =>
        [.. GetPartitions().Where(partition => _partitionPolicyService.IsCachePartition(ToState(partition)))];

    public PartitionConfig GetPrimary()
    {
        List<PartitionConfig> available = GetPartitions();
        int id = _partitionPolicyService.GetRequiredPartitionIdByType(
            BuildPartitionStates(available),
            "primary"
        );
        return _partitions[id].Partition;
    }

    public PartitionConfig GetHeavy()
    {
        List<PartitionConfig> available = GetPartitions();
        int id = _partitionPolicyService.GetRequiredPartitionIdByType(
            BuildPartitionStates(available),
            "heavy"
        );
        return _partitions[id].Partition;
    }

    private List<PartitionState> BuildPartitionStates(IReadOnlyList<PartitionConfig> partitions) =>
        partitions.Select(ToState).ToList();

    private PartitionState ToState(PartitionConfig partition)
    {
        _partitions.TryGetValue(partition.Id, out PartitionSize? current);
        return new PartitionState
        {
            Id = partition.Id,
            Type = partition.Type,
            Tags = partition.Tags,
            Size = partition.Size,
            UsableSpace = partition.UsableSpace,
            Enabled = partition.Enabled,
            CurrentSize = current?.Size ?? 0,
        };
    }
}
