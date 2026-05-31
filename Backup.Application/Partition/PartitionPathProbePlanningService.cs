using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public sealed class PartitionPathProbePlanningService : IPartitionPathProbePlanningService
{
    public IReadOnlyList<PartitionPathProbeTarget> BuildTargets(
        IEnumerable<PartitionPathProbeCandidate> partitions
    )
    {
        List<PartitionPathProbeTarget> targets = [];

        foreach (PartitionPathProbeCandidate partition in partitions)
        {
            if (!partition.Enabled)
                continue;

            string fileName = $"{Guid.NewGuid():N}";
            targets.Add(
                new PartitionPathProbeTarget
                {
                    PartitionName = partition.PartitionName,
                    ProbePath = Path.Combine(partition.RootPath, fileName),
                }
            );
        }

        return targets;
    }
}
