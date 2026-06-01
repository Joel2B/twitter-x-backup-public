using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;

namespace Backup.Tests;

public class BulkPrunePolicyServiceTests
{
    private readonly BulkPrunePolicyService _sut = new();

    [Fact]
    public void GetPathsToRemove_ReturnsEmpty_WhenNoPaths()
    {
        IReadOnlyList<string> paths = _sut.GetPathsToRemove([], keepDays: 7);

        Assert.Empty(paths);
    }

    [Fact]
    public void GetPathsToRemove_RemovesOnlyOlderThanThreshold()
    {
        List<DatedPath> datedPaths =
        [
            new("a.json", new DateTime(2026, 5, 10)),
            new("b.json", new DateTime(2026, 5, 14)),
            new("c.json", new DateTime(2026, 5, 15)),
        ];

        IReadOnlyList<string> paths = _sut.GetPathsToRemove(datedPaths, keepDays: 1);

        Assert.Equal(["a.json"], paths);
    }

    [Fact]
    public void GetPathsToRemove_KeepsAll_WhenKeepDaysNegative()
    {
        List<DatedPath> datedPaths =
        [
            new("a.json", new DateTime(2026, 5, 14)),
            new("b.json", new DateTime(2026, 5, 15)),
        ];

        IReadOnlyList<string> paths = _sut.GetPathsToRemove(datedPaths, keepDays: -10);

        Assert.Equal(["a.json"], paths);
    }
}
