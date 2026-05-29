using Backup.Application.Posts;
using Backup.Application.Posts.Models;

namespace Backup.Tests;

public class PostRecoverySelectionServiceTests
{
    private readonly IPostRecoverySelectionService _service = new PostRecoverySelectionService();

    [Fact]
    public void Select_ReturnsDisabled_WhenRecoveryIsDisabled()
    {
        PostRecoverySelection selection = _service.Select(
            recoveryEnabled: false,
            logs:
            [
                new PostRecoveryLog { PostId = "1", Messages = ["NotFound"] },
            ],
            maxPosts: 10
        );

        Assert.False(selection.IsRecoveryEnabled);
        Assert.Empty(selection.PostIds);
    }

    [Fact]
    public void Select_ReturnsOnlyNotFoundOrForbidden_AndLimitsAndDeduplicates()
    {
        PostRecoverySelection selection = _service.Select(
            recoveryEnabled: true,
            logs:
            [
                new PostRecoveryLog { PostId = "1", Messages = ["NotFound"] },
                new PostRecoveryLog { PostId = "1", Messages = ["Forbidden"] },
                new PostRecoveryLog { PostId = "2", Messages = ["Other"] },
                new PostRecoveryLog { PostId = "3", Messages = ["forbidden"] },
                new PostRecoveryLog { PostId = "4", Messages = ["NOTFOUND"] },
            ],
            maxPosts: 2
        );

        Assert.True(selection.IsRecoveryEnabled);
        Assert.Equal(2, selection.PostIds.Count);
        Assert.Equal("1", selection.PostIds[0]);
        Assert.Equal("3", selection.PostIds[1]);
    }
}
