using Backup.Infrastructure.Media.Maintenance;

namespace Backup.Tests.Media.Maintenance;

public sealed class AutomaticLooseMediaPruneServiceTests
{
    [Theory]
    [InlineData("1/photo/2/source/jpg/size/orig.jpg", "1/photo/2/source")]
    [InlineData("1/video/2/thumb/source/jpg/size/orig.jpg", "1/video/2/thumb/source")]
    [InlineData("1/gif/2/thumb/source/png/size/orig.png", "1/gif/2/thumb/source")]
    [InlineData("1/video/2/mp4/source/1920x1080.mp4", "1/video/2")]
    public void Group_keeps_media_boundaries(string path, string expected)
    {
        Assert.Equal(expected, AutomaticLooseMediaPruneService.Group(path));
    }

    [Fact]
    public void Normalize_accepts_cache_separator_variants()
    {
        Assert.Equal(
            "1/photo/2/source/jpg/size/orig.jpg",
            AutomaticLooseMediaPruneService.Normalize(
                "1\\photo\\2\\source\\jpg\\size\\orig.jpg"
            )
        );
    }
}
