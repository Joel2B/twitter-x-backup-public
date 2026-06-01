using Backup.Application.Partition;
using Backup.Application.Partition.Models;

namespace Backup.Tests;

public sealed class PartitionPathProbePlanningServiceTests
{
    [Fact]
    public void BuildTargets_SkipsDisabledPartitions()
    {
        PartitionPathProbePlanningService sut = new();

        IReadOnlyList<PartitionPathProbeTarget> targets = sut.BuildTargets(
            [
                new PartitionPathProbeCandidate
                {
                    PartitionName = "p1",
                    Enabled = true,
                    RootPath = Path.Combine("root", "one"),
                },
                new PartitionPathProbeCandidate
                {
                    PartitionName = "p2",
                    Enabled = false,
                    RootPath = Path.Combine("root", "two"),
                },
            ]
        );

        Assert.Single(targets);
        Assert.Equal("p1", targets[0].PartitionName);
        Assert.Equal(Path.Combine("root", "one"), Path.GetDirectoryName(targets[0].ProbePath));
        Assert.False(string.IsNullOrWhiteSpace(Path.GetFileName(targets[0].ProbePath)));
    }
}
