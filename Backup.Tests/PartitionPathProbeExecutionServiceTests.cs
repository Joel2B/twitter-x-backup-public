using Backup.Application.Partition;
using Backup.Application.Partition.Models;

namespace Backup.Tests;

public sealed class PartitionPathProbeExecutionServiceTests
{
    [Fact]
    public void Execute_ProbesTargetsAndReturnsResultSummary()
    {
        FakePlanningService planning = new(
            [
                new PartitionPathProbeTarget { PartitionName = "p1", ProbePath = "/tmp/1" },
                new PartitionPathProbeTarget { PartitionName = "p2", ProbePath = "/tmp/2" },
            ]
        );
        FakeProbeService probe = new(
            new Dictionary<string, string?> { ["/tmp/1"] = null, ["/tmp/2"] = "error" }
        );
        PartitionPathProbeExecutionService sut = new(planning, probe);

        PartitionPathProbeExecutionResult result = sut.Execute(
            [
                new PartitionPathProbeCandidate
                {
                    PartitionName = "ignored",
                    Enabled = true,
                    RootPath = "/any",
                },
            ]
        );

        Assert.Single(planning.CapturedInputs);
        Assert.Equal(2, probe.Calls.Count);
        Assert.Equal(2, result.Results.Count);
        Assert.True(result.HasErrors);
        Assert.Equal("p1", result.Results[0].PartitionName);
        Assert.Null(result.Results[0].Error);
        Assert.Equal("p2", result.Results[1].PartitionName);
        Assert.Equal("error", result.Results[1].Error);
    }

    private sealed class FakePlanningService(IReadOnlyList<PartitionPathProbeTarget> targets)
        : IPartitionPathProbePlanningService
    {
        private readonly IReadOnlyList<PartitionPathProbeTarget> _targets = targets;
        public List<PartitionPathProbeCandidate> CapturedInputs { get; } = [];

        public IReadOnlyList<PartitionPathProbeTarget> BuildTargets(
            IEnumerable<PartitionPathProbeCandidate> partitions
        )
        {
            CapturedInputs.AddRange(partitions);
            return _targets;
        }
    }

    private sealed class FakeProbeService(IReadOnlyDictionary<string, string?> errorsByPath)
        : IPartitionPathProbeService
    {
        private readonly IReadOnlyDictionary<string, string?> _errorsByPath = errorsByPath;
        public List<string> Calls { get; } = [];

        public string? Probe(string path)
        {
            Calls.Add(path);
            return _errorsByPath.TryGetValue(path, out string? error) ? error : null;
        }
    }
}
