using Backup.Application.Media.Prune;

namespace Backup.Tests;

public class MediaPruneSelectionServiceTests
{
    [Fact]
    public void ShouldRemove_ReturnsFalse_WhenRuleMatchesQuery()
    {
        MediaPruneSelectionService sut = new(new MediaPrunePolicyService(["jpg:large:small"]));
        string url = "https://pbs.twimg.com/media/test.jpg?format=large&name=small";

        bool shouldRemove = sut.ShouldRemove(url, "test.jpg");

        Assert.False(shouldRemove);
    }

    [Fact]
    public void ShouldRemove_ReturnsFalse_WhenRuleMatchesPathExtension()
    {
        MediaPruneSelectionService sut = new(new MediaPrunePolicyService(["mp4:*:*"]));
        string url = "https://video.twimg.com/ext_tw_video/12345";

        bool shouldRemove = sut.ShouldRemove(url, "video.mp4");

        Assert.False(shouldRemove);
    }

    [Fact]
    public void ShouldRemove_ReturnsTrue_WhenNoRuleMatches()
    {
        MediaPruneSelectionService sut = new(new MediaPrunePolicyService(["png:*:*"]));
        string url = "https://pbs.twimg.com/media/test.jpg?format=large&name=small";

        bool shouldRemove = sut.ShouldRemove(url, "test.jpg");

        Assert.True(shouldRemove);
    }
}
