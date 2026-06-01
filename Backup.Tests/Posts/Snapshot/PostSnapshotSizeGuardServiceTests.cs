using Backup.Application.Posts;

namespace Backup.Tests;

public class PostSnapshotSizeGuardServiceTests
{
    private readonly PostSnapshotSizeGuardService _sut = new();

    [Fact]
    public void EnsureNotShrunkBeyondThreshold_DoesNothing_WhenDiffWithinThreshold()
    {
        _sut.EnsureNotShrunkBeyondThreshold(
            currentLength: 900,
            historyLength: 1000,
            threshold: 100,
            fileName: "posts.json",
            historyDirectoryName: "2026-05-30"
        );
    }

    [Fact]
    public void EnsureNotShrunkBeyondThreshold_Throws_WhenDiffExceedsThreshold()
    {
        Exception ex = Assert.Throws<Exception>(
            () =>
                _sut.EnsureNotShrunkBeyondThreshold(
                    currentLength: 700,
                    historyLength: 1000,
                    threshold: 100,
                    fileName: "posts.json",
                    historyDirectoryName: "2026-05-30"
                )
        );

        Assert.Contains("posts.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureNotShrunkBeyondThreshold_NormalizesNegativeThresholdToZero()
    {
        Exception ex = Assert.Throws<Exception>(
            () =>
                _sut.EnsureNotShrunkBeyondThreshold(
                    currentLength: 999,
                    historyLength: 1000,
                    threshold: -10,
                    fileName: "posts.json",
                    historyDirectoryName: "2026-05-30"
                )
        );

        Assert.Contains("threshold=0", ex.Message, StringComparison.Ordinal);
    }
}
