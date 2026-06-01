using Backup.Application.Media.Backup;

namespace Backup.Tests;

public class MediaBackupPathAnalysisServiceTests
{
    [Fact]
    public void FindDuplicates_ReturnsOnlyDuplicatedPaths()
    {
        MediaBackupPathAnalysisService sut = new();
        string[] paths = ["a.jpg", "b.jpg", "a.jpg", "c.jpg", "b.jpg"];

        var duplicates = sut.FindDuplicates(paths);

        Assert.Equal(2, duplicates.Count);
        Assert.Contains(duplicates, group => group.Path == "a.jpg" && group.Count == 2);
        Assert.Contains(duplicates, group => group.Path == "b.jpg" && group.Count == 2);
    }

    [Fact]
    public void Diff_ReturnsMissingAndExtras()
    {
        MediaBackupPathAnalysisService sut = new();
        string[] expected = ["a.jpg", "b.jpg", "d.jpg"];
        string[] actual = ["a.jpg", "c.jpg", "b.jpg"];

        var diff = sut.Diff(expected, actual);

        Assert.Single(diff.MissingPaths);
        Assert.Equal("d.jpg", diff.MissingPaths[0]);
        Assert.Single(diff.ExtraPaths);
        Assert.Equal("c.jpg", diff.ExtraPaths[0]);
    }
}
