using Backup.Application.Posts;
using Backup.Application.Posts.Models;

namespace Backup.Tests;

public class PostHistoryPrunePolicyServiceTests
{
    private readonly PostHistoryPrunePolicyService _sut = new();

    [Fact]
    public void GetPathsToRemove_ReturnsEmpty_WhenNoPaths()
    {
        IReadOnlyList<string> remove = _sut.GetPathsToRemove([], keepDays: 3, keepCount: 2);

        Assert.Empty(remove);
    }

    [Fact]
    public void GetPathsToRemove_RemovesOldDays_AndExtraWithinKeptDay()
    {
        List<PostHistoryPath> paths =
        [
            new("d1-a", new DateTime(2026, 5, 1, 1, 0, 0)),
            new("d1-b", new DateTime(2026, 5, 1, 2, 0, 0)),
            new("d2-a", new DateTime(2026, 5, 2, 1, 0, 0)),
            new("d2-b", new DateTime(2026, 5, 2, 2, 0, 0)),
            new("d2-c", new DateTime(2026, 5, 2, 3, 0, 0)),
            new("d3-a", new DateTime(2026, 5, 3, 1, 0, 0)),
        ];

        IReadOnlyList<string> remove = _sut.GetPathsToRemove(paths, keepDays: 2, keepCount: 2);

        Assert.Equal(["d1-a", "d1-b", "d2-a"], remove);
    }

    [Fact]
    public void GetPathsToRemove_NormalizesNegativeValues()
    {
        List<PostHistoryPath> paths =
        [
            new("d1-a", new DateTime(2026, 5, 1, 1, 0, 0)),
            new("d2-a", new DateTime(2026, 5, 2, 1, 0, 0)),
        ];

        IReadOnlyList<string> remove = _sut.GetPathsToRemove(paths, keepDays: -1, keepCount: -5);

        Assert.Equal(["d1-a", "d2-a"], remove);
    }
}
