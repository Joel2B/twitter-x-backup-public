using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public sealed class PartitionResolutionService(
    IPartitionStateProjectionService partitionStateProjectionService,
    IPartitionPolicyService partitionPolicyService
) : IPartitionResolutionService
{
    private readonly IPartitionStateProjectionService _partitionStateProjectionService =
        partitionStateProjectionService;
    private readonly IPartitionPolicyService _partitionPolicyService = partitionPolicyService;

    public IReadOnlyCollection<int> SelectEnabledIds(
        IEnumerable<PartitionStateSource> sources,
        IReadOnlyCollection<int>? selectedIds
    )
    {
        IReadOnlyList<PartitionState> states = _partitionStateProjectionService.ToStates(sources);
        IReadOnlyList<PartitionState> filtered = _partitionPolicyService.FilterEnabled(
            states,
            selectedIds?.ToList()
        );
        return filtered.Select(state => state.Id).ToList();
    }

    public int ResolvePartitionId(IEnumerable<PartitionStateSource> sources, int? requestedId, long size)
    {
        IReadOnlyList<PartitionState> states = _partitionStateProjectionService.ToStates(sources);
        return _partitionPolicyService.ResolvePartitionId(states, requestedId, size);
    }

    public IReadOnlyCollection<int> SelectCacheIds(IEnumerable<PartitionStateSource> sources)
    {
        IReadOnlyList<PartitionState> states = _partitionStateProjectionService.ToStates(sources);
        return states
            .Where(state => _partitionPolicyService.IsCachePartition(state))
            .Select(state => state.Id)
            .ToList();
    }

    public int GetRequiredPartitionIdByType(IEnumerable<PartitionStateSource> sources, string type)
    {
        IReadOnlyList<PartitionState> states = _partitionStateProjectionService.ToStates(sources);
        return _partitionPolicyService.GetRequiredPartitionIdByType(states, type);
    }
}
