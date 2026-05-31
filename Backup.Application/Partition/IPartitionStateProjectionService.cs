using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public interface IPartitionStateProjectionService
{
    PartitionState ToState(PartitionStateSource source);
    IReadOnlyList<PartitionState> ToStates(IEnumerable<PartitionStateSource> sources);
}
