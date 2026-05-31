using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public interface IPartitionPathProbePlanningService
{
    IReadOnlyList<PartitionPathProbeTarget> BuildTargets(
        IEnumerable<PartitionPathProbeCandidate> partitions
    );
}
