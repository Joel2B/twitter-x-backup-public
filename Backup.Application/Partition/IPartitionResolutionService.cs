using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public interface IPartitionResolutionService
{
    IReadOnlyCollection<int> SelectEnabledIds(
        IEnumerable<PartitionStateSource> sources,
        IReadOnlyCollection<int>? selectedIds
    );

    int ResolvePartitionId(
        IEnumerable<PartitionStateSource> sources,
        int? requestedId,
        long size
    );

    IReadOnlyCollection<int> SelectCacheIds(IEnumerable<PartitionStateSource> sources);

    int GetRequiredPartitionIdByType(IEnumerable<PartitionStateSource> sources, string type);
}
