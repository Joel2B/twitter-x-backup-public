using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public sealed class PartitionPathProbeExecutionService(
    IPartitionPathProbePlanningService partitionPathProbePlanningService,
    IPartitionPathProbeService partitionPathProbeService
) : IPartitionPathProbeExecutionService
{
    private readonly IPartitionPathProbePlanningService _partitionPathProbePlanningService =
        partitionPathProbePlanningService;
    private readonly IPartitionPathProbeService _partitionPathProbeService =
        partitionPathProbeService;

    public PartitionPathProbeExecutionResult Execute(
        IEnumerable<PartitionPathProbeCandidate> partitions
    )
    {
        IReadOnlyList<PartitionPathProbeTarget> targets =
            _partitionPathProbePlanningService.BuildTargets(partitions);
        List<PartitionPathProbeResult> results = [];
        bool hasErrors = false;

        foreach (PartitionPathProbeTarget target in targets)
        {
            string? error = _partitionPathProbeService.Probe(target.ProbePath);
            results.Add(
                new PartitionPathProbeResult { PartitionName = target.PartitionName, Error = error }
            );

            if (error is not null)
                hasErrors = true;
        }

        return new PartitionPathProbeExecutionResult { Results = results, HasErrors = hasErrors };
    }
}
