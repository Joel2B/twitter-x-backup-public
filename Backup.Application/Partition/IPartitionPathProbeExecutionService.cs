using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public interface IPartitionPathProbeExecutionService
{
    PartitionPathProbeExecutionResult Execute(IEnumerable<PartitionPathProbeCandidate> partitions);
}
