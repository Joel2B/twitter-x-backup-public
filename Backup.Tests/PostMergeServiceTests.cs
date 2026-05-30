using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Tests;

public class PostMergeServiceTests
{
    [Fact]
    public void Merge_AppliesProfileFallbackAndDataChange()
    {
        Post current = CreatePost(
            description: "old description",
            userName: "legacy_user",
            previous: "p1",
            next: "n1"
        );
        Post incoming = CreatePost(
            description: "new description",
            userName: null,
            previous: "p2",
            next: "n2"
        );

        PostMergeService service = new();
        PostMergeOutcome outcome = service.Merge(
            "user-1",
            "origin-a",
            current,
            incoming,
            new MergeOptions { Index = true }
        );

        Assert.True(outcome.HasDataChange);
        Assert.True(outcome.HasIndexChange);
        Assert.True(outcome.HasChanges);
        Assert.NotNull(outcome.Change);
        Assert.Equal("legacy_user", outcome.MergedPost.Profile.UserName);
        Assert.Equal("new description", outcome.MergedPost.Description);
        Assert.Equal("p2", outcome.MergedPost.Index["user-1"]["origin-a"].Previous);
        Assert.Equal("n2", outcome.MergedPost.Index["user-1"]["origin-a"].Next);
        Assert.Equal("user-1", outcome.Change!.UserId);
        Assert.NotNull(outcome.Change.Data);
        Assert.NotNull(outcome.Change.Index);
    }

    [Fact]
    public void Merge_WhenNoChanges_DoesNotAppendChange()
    {
        Post current = CreatePost(
            description: "same description",
            userName: "same_user",
            previous: "p1",
            next: "n1"
        );
        Post incoming = CreatePost(
            description: "same description",
            userName: "same_user",
            previous: "p1",
            next: "n1"
        );

        PostMergeService service = new();
        PostMergeOutcome outcome = service.Merge(
            "user-1",
            "origin-a",
            current,
            incoming,
            new MergeOptions { Index = true }
        );

        Assert.False(outcome.HasDataChange);
        Assert.False(outcome.HasIndexChange);
        Assert.False(outcome.HasChanges);
        Assert.Null(outcome.Change);
        Assert.Empty(outcome.MergedPost.Changes);
    }

    [Fact]
    public void Merge_WhenIndexDisabled_DoesNotCaptureIndexChange()
    {
        Post current = CreatePost(
            description: "same description",
            userName: "same_user",
            previous: "p1",
            next: "n1"
        );
        Post incoming = CreatePost(
            description: "same description",
            userName: "same_user",
            previous: "p2",
            next: "n2"
        );

        PostMergeService service = new();
        PostMergeOutcome outcome = service.Merge(
            "user-1",
            "origin-a",
            current,
            incoming,
            new MergeOptions { Index = false }
        );

        Assert.False(outcome.HasIndexChange);
        Assert.False(outcome.HasChanges);
    }

    private static Post CreatePost(
        string description,
        string? userName,
        string? previous,
        string? next
    ) =>
        new()
        {
            Id = "post-1",
            Profile = new PostProfile
            {
                Id = "profile-1",
                UserName = userName,
                Name = "name",
                BannerUrl = "banner",
                ImageUrl = "image",
                Following = true,
            },
            Description = description,
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "Sun May 24 08:40:16 +0000 2026",
            Hashtags = ["tag1"],
            Medias =
            [
                new PostMedia
                {
                    Id = "media-1",
                    Url = "https://pbs.twimg.com/media/x.jpg",
                    Type = "photo",
                    VideoInfo = null,
                },
            ],
            Deleted = false,
            Changes = [],
            Index = new Dictionary<string, Dictionary<string, IndexData>>
            {
                ["user-1"] = new Dictionary<string, IndexData>
                {
                    ["origin-a"] = new IndexData { Previous = previous, Next = next },
                },
            },
        };
}
