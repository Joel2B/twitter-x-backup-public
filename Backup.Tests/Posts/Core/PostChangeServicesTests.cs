using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Tests;

public class PostChangeServicesTests
{
    [Fact]
    public void Compute_UsesCanonicalNamesForPersistedFields()
    {
        Post post = CreatePost();
        PostData oldData = post.Clone();
        oldData.CreatedAt = "old-created-at";
        oldData.Profile.UserName = "old-user-name";
        oldData.Profile.BannerUrl = "old-banner";
        oldData.Profile.ImageUrl = "old-image";
        oldData.Profile.Count = new PostCount { Media = 3 };
        post.Changes.Add(
            new Change { UserId = "user", Date = new DateTime(2026, 1, 1), Data = oldData }
        );

        PostComputedChange change = Assert.Single(new PostChangeComputationService().Compute(post));

        Assert.Equal(
            [
                "post.created_at",
                "profile.user_name",
                "profile.banner_url",
                "profile.image_url",
                "profile.count_media",
            ],
            change.Fields.Select(field => field.Field)
        );
    }

    [Fact]
    public void Project_ReplaysCanonicalPersistedFields()
    {
        Post post = CreatePost();
        PostChangeReplayEntry entry = new()
        {
            UserId = "user",
            Date = new DateTime(2026, 1, 1),
            Sequence = 1,
            Fields =
            [
                Field("post.created_at", "\"old-created-at\""),
                Field("profile.user_name", "\"old-user-name\""),
                Field("profile.banner_url", "\"old-banner\""),
                Field("profile.image_url", "\"old-image\""),
                Field("profile.count_media", "3"),
            ],
        };

        Change change = Assert.Single(
            new PostChangeReadModelProjectionService().Project(post, [entry])
        );

        Assert.Equal("old-created-at", change.Data!.CreatedAt);
        Assert.Equal("old-user-name", change.Data.Profile.UserName);
        Assert.Equal("old-banner", change.Data.Profile.BannerUrl);
        Assert.Equal("old-image", change.Data.Profile.ImageUrl);
        Assert.Equal(3, change.Data.Profile.Count!.Media);
    }

    [Fact]
    public void Project_ReplaysNullMediaCount()
    {
        Post post = CreatePost();
        PostChangeReplayEntry entry = new()
        {
            UserId = "user",
            Date = new DateTime(2026, 1, 1),
            Sequence = 1,
            Fields = [Field("profile.count_media", null)],
        };

        Change change = Assert.Single(
            new PostChangeReadModelProjectionService().Project(post, [entry])
        );

        Assert.Null(change.Data!.Profile.Count);
    }

    [Fact]
    public void Project_ReplaysIndexForDottedUserId()
    {
        const string userId = "user.name.example";
        Post post = CreatePost();
        post.Index[userId] = new Dictionary<string, IndexData>
        {
            ["current"] = new() { Previous = "current-previous" },
        };
        PostChangeReplayEntry entry = new()
        {
            UserId = userId,
            Date = new DateTime(2026, 1, 1),
            Sequence = 1,
            Fields =
            [
                Field(
                    $"index.{userId}",
                    "{\"old\":{\"Previous\":\"old-previous\",\"Next\":null}}"
                ),
            ],
        };

        Change change = Assert.Single(
            new PostChangeReadModelProjectionService().Project(post, [entry])
        );

        KeyValuePair<string, IndexData> oldIndex = Assert.Single(change.Index!);
        Assert.Equal("old", oldIndex.Key);
        Assert.Equal("old-previous", oldIndex.Value.Previous);
    }

    private static PostChangeReplayField Field(string field, string? oldValueJson) =>
        new() { Field = field, OldValueJson = oldValueJson };

    private static Post CreatePost() =>
        new()
        {
            Id = "post",
            Description = "description",
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "current-created-at",
            Profile = new PostProfile
            {
                Id = "profile",
                UserName = "current-user-name",
                BannerUrl = "current-banner",
                ImageUrl = "current-image",
                Count = new PostCount { Media = 10 },
            },
        };
}
