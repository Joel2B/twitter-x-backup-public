using Backup.Application.Media;

namespace Backup.Tests;

public sealed class MediaPathSelectionServiceTests
{
    [Fact]
    public void SelectRequiredRootPath_ReturnsFirstNonEmptyPath()
    {
        MediaPathSelectionService sut = new();

        string path = sut.SelectRequiredRootPath(["", " ", @"E:\media", @"F:\media"]);

        Assert.Equal(@"E:\media", path);
    }

    [Fact]
    public void SelectRequiredRootPath_ThrowsWhenMissing()
    {
        MediaPathSelectionService sut = new();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => sut.SelectRequiredRootPath(["", " "])
        );

        Assert.Equal("No media root path is configured.", ex.Message);
    }
}
