using Backup.Application.Posts;
using Backup.Application.Posts.Models;

namespace Backup.Tests;

public class PostDebugLogPrunePolicyServiceTests
{
    private readonly PostDebugLogPrunePolicyService _sut = new();

    [Fact]
    public void GetPathsToRemove_ReturnsEmpty_WhenNoPaths()
    {
        IReadOnlyList<string> remove = _sut.GetPathsToRemove([], retainedCountLimit: 3);

        Assert.Empty(remove);
    }

    [Fact]
    public void GetPathsToRemove_KeepLatestByRetainedLimit()
    {
        List<PostHistoryPath> paths =
        [
            new("a", new DateTime(2026, 5, 30, 1, 0, 0)),
            new("b", new DateTime(2026, 5, 30, 2, 0, 0)),
            new("c", new DateTime(2026, 5, 30, 3, 0, 0)),
            new("d", new DateTime(2026, 5, 30, 4, 0, 0)),
        ];

        IReadOnlyList<string> remove = _sut.GetPathsToRemove(paths, retainedCountLimit: 2);

        Assert.Equal(["a", "b"], remove);
    }

    [Fact]
    public void GetPathsToRemove_NegativeRetainedCount_RemovesAll()
    {
        List<PostHistoryPath> paths =
        [
            new("a", new DateTime(2026, 5, 30, 1, 0, 0)),
            new("b", new DateTime(2026, 5, 30, 2, 0, 0)),
        ];

        IReadOnlyList<string> remove = _sut.GetPathsToRemove(paths, retainedCountLimit: -1);

        Assert.Equal(["a", "b"], remove);
    }
}
