using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;

namespace Backup.Tests;

public sealed class MediaCacheRecheckPlanningServiceTests
{
    [Fact]
    public void SelectPathsToRecheck_ProjectsEntriesAndDelegatesSelection()
    {
        FakeOrchestration orchestration = new(["/a.jpg"]);
        MediaCacheRecheckPlanningService sut = new(orchestration);
        List<MediaCacheStoredEntry> entries =
        [
            new()
            {
                Path = "/a.jpg",
                PartitionId = 1,
                StreamSizeBytes = 100,
                FileSizeBytes = null,
            },
        ];

        IReadOnlyCollection<string> result = sut.SelectPathsToRecheck(entries);

        Assert.NotNull(orchestration.LastCandidates);
        Assert.Single(orchestration.LastCandidates);
        Assert.Equal("/a.jpg", orchestration.LastCandidates.First().Path);
        Assert.Equal(100, orchestration.LastCandidates.First().StreamSizeBytes);
        Assert.Null(orchestration.LastCandidates.First().FileSizeBytes);
        Assert.Single(result);
        Assert.Equal("/a.jpg", result.First());
    }

    private sealed class FakeOrchestration(IReadOnlyCollection<string> result)
        : IMediaCacheRecheckOrchestrationService
    {
        private readonly IReadOnlyCollection<string> _result = result;
        public IReadOnlyCollection<MediaCacheRecheckCandidate>? LastCandidates { get; private set; }

        public IReadOnlyCollection<string> SelectRecheckPaths(
            IReadOnlyCollection<MediaCacheRecheckCandidate> candidates
        )
        {
            LastCandidates = candidates;
            return _result;
        }

        public MediaCacheRecheckResult Evaluate(MediaCacheRecheckObservation observation) =>
            new() { IsInvalid = true };
    }
}
