using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public interface IPartitionPolicyService
{
    IReadOnlyList<PartitionState> FilterEnabled(
        IReadOnlyList<PartitionState> partitions,
        IReadOnlyCollection<int>? allowedIds
    );

    int GetRequiredPartitionIdByType(IReadOnlyList<PartitionState> partitions, string type);

    int ResolvePartitionId(IReadOnlyList<PartitionState> partitions, int? requestedId, long size);

    bool IsCachePartition(PartitionState partition);
}
