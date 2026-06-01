using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;

namespace Backup.Tests;

public sealed class MediaCacheRecheckPlanningServiceTests
{
    [Fact]
    public void SelectPathsToRecheck_ComposesProjectionAndSelection()
    {
        FakeProjection projection = new(
            [
                new MediaCacheRecheckCandidate
                {
                    Path = "/a.jpg",
                    StreamSizeBytes = 100,
                    FileSizeBytes = null,
                },
            ]
        );
        FakeOrchestration orchestration = new(["/a.jpg"]);
        MediaCacheRecheckPlanningService sut = new(projection, orchestration);
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

        Assert.NotNull(projection.LastEntries);
        Assert.Single(projection.LastEntries);
        Assert.Equal("/a.jpg", projection.LastEntries.First().Path);
        Assert.NotNull(orchestration.LastCandidates);
        Assert.Single(orchestration.LastCandidates);
        Assert.Equal("/a.jpg", orchestration.LastCandidates.First().Path);
        Assert.Single(result);
        Assert.Equal("/a.jpg", result.First());
    }

    private sealed class FakeProjection(IReadOnlyList<MediaCacheRecheckCandidate> candidates)
        : IMediaCacheStoredEntryProjectionService
    {
        private readonly IReadOnlyList<MediaCacheRecheckCandidate> _candidates = candidates;
        public IReadOnlyCollection<MediaCacheStoredEntry>? LastEntries { get; private set; }

        public IReadOnlyList<MediaCacheRecheckCandidate> ToRecheckCandidates(
            IEnumerable<MediaCacheStoredEntry> entries
        )
        {
            LastEntries = entries.ToList();
            return _candidates;
        }

        public IEnumerable<KeyValuePair<int?, long?>> ToPartitionFileSizes(
            IEnumerable<MediaCacheStoredEntry> entries
        ) => [];
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
