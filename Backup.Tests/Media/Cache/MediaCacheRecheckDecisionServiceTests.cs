using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;

namespace Backup.Tests;

public sealed class MediaCacheRecheckDecisionServiceTests
{
    [Fact]
    public void Decide_ComposesEvaluateAndApply()
    {
        FakeOrchestrationService orchestration = new(
            new MediaCacheRecheckResult
            {
                ShouldUpdate = true,
                PartitionId = 2,
                StreamSizeBytes = 111,
                FileSizeBytes = 99,
            }
        );
        FakeApplyPolicy applyPolicy = new(
            new MediaCacheRecheckApplyResult
            {
                UpdatedEntryState = new MediaCacheEntryState
                {
                    Path = "/media/a.jpg",
                    PartitionId = 2,
                    StreamSizeBytes = 111,
                    FileSizeBytes = 99,
                },
            }
        );
        MediaCacheRecheckDecisionService sut = new(orchestration, applyPolicy);
        MediaCacheRecheckObservation observation = new()
        {
            Path = "/media/a.jpg",
            PartitionId = 2,
            StreamSizeBytes = 111,
            FileExists = true,
            FileSizeBytes = 99,
        };

        MediaCacheRecheckApplyResult result = sut.Decide(observation);

        Assert.Same(observation, orchestration.LastObservation);
        Assert.Equal("/media/a.jpg", applyPolicy.LastPath);
        Assert.Equal(2, applyPolicy.LastDecision?.PartitionId);
        Assert.NotNull(result.UpdatedEntryState);
        Assert.Equal(2, result.UpdatedEntryState.PartitionId);
    }

    private sealed class FakeOrchestrationService(MediaCacheRecheckResult decision)
        : IMediaCacheRecheckOrchestrationService
    {
        private readonly MediaCacheRecheckResult _decision = decision;
        public MediaCacheRecheckObservation? LastObservation { get; private set; }

        public IReadOnlyCollection<string> SelectRecheckPaths(
            IReadOnlyCollection<MediaCacheRecheckCandidate> candidates
        ) => candidates.Select(c => c.Path).ToList();

        public MediaCacheRecheckResult Evaluate(MediaCacheRecheckObservation observation)
        {
            LastObservation = observation;
            return _decision;
        }
    }

    private sealed class FakeApplyPolicy(MediaCacheRecheckApplyResult result)
        : IMediaCacheRecheckApplyPolicyService
    {
        private readonly MediaCacheRecheckApplyResult _result = result;
        public string? LastPath { get; private set; }
        public MediaCacheRecheckResult? LastDecision { get; private set; }

        public MediaCacheRecheckApplyResult Apply(string path, MediaCacheRecheckResult decision)
        {
            LastPath = path;
            LastDecision = decision;
            return _result;
        }
    }
}
