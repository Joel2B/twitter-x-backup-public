using Backup.Application.Media.Backup;

namespace Backup.Tests;

public sealed class MediaBackupIntegrityChunkDataSelectionServiceTests
{
    [Fact]
    public void Select_Returns_Selected_And_Missing_Paths()
    {
        MediaBackupIntegrityChunkDataSelectionService sut = new();

        var result = sut.Select(
            changedPaths: ["a.jpg", "b.jpg", "x.jpg"],
            chunkPaths: ["a.jpg", "b.jpg", "c.jpg"]
        );

        Assert.Equal(2, result.SelectedPaths.Count);
        Assert.Contains("a.jpg", result.SelectedPaths);
        Assert.Contains("b.jpg", result.SelectedPaths);
        Assert.Single(result.MissingPaths);
        Assert.Contains("x.jpg", result.MissingPaths);
        Assert.False(result.IsComplete);
    }
}
