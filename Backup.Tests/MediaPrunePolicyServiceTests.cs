using Backup.Application.Media.Prune;

namespace Backup.Tests;

public class MediaPrunePolicyServiceTests
{
    [Fact]
    public void ShouldKeep_ReturnsTrue_WhenFilterMatches()
    {
        MediaPrunePolicyService sut = new(["jpg:*:*"]);

        bool keep = sut.ShouldKeep("jpg", "large", "4096x4096");

        Assert.True(keep);
    }

    [Fact]
    public void ShouldKeep_ReturnsFalse_WhenNoFilterMatches()
    {
        MediaPrunePolicyService sut = new(["png:*:small"]);

        bool keep = sut.ShouldKeep("jpg", "large", "4096x4096");

        Assert.False(keep);
    }

    [Fact]
    public void ShouldKeep_IsCaseInsensitive()
    {
        MediaPrunePolicyService sut = new(["JPG:LaRgE:*"]);

        bool keep = sut.ShouldKeep("jpg", "large", "any");

        Assert.True(keep);
    }
}
