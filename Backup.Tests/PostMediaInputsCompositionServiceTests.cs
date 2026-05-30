using Backup.Application.Posts;
using Backup.Domain.Posts;

namespace Backup.Tests;

public class PostMediaInputsCompositionServiceTests
{
    [Fact]
    public void Compose_ReturnsCurrentAndHistoryMediaInputs()
    {
        Post post = CreatePost("p1", deleted: false);
        post.Changes.Add(
            new Change
            {
                UserId = "user-1",
                Data = new PostData
                {
                    Id = "p1",
                    Profile = new PostProfile { Id = "profile-1", UserName = "history-user" },
                    Description = "history",
                    Retweeted = false,
                    Favorited = false,
                    Bookmarked = false,
                    CreatedAt = "2025-01-01T00:00:00Z",
                    Hashtags = [],
                    Medias = [new PostMedia { Id = "m-history", Url = "https://cdn/h.jpg", Type = "photo" }],
                    Deleted = true,
                },
            }
        );

        PostMediaInputsCompositionService service = new();
        IReadOnlyList<MediaInput> result = service.Compose([post]);

        Assert.Equal(2, result.Count);
        Assert.Equal("p1", result[0].Id);
        Assert.False(result[0].Deleted);
        Assert.Equal("p1", result[1].Id);
        Assert.True(result[1].Deleted);
        Assert.Equal("m-history", result[1].Medias![0].Id);
    }

    [Fact]
    public void Compose_EmptyPosts_ReturnsEmpty()
    {
        PostMediaInputsCompositionService service = new();
        IReadOnlyList<MediaInput> result = service.Compose([]);
        Assert.Empty(result);
    }

    private static Post CreatePost(string id, bool deleted) =>
        new()
        {
            Id = id,
            Profile = new PostProfile { Id = "profile-1", UserName = "current-user" },
            Description = "current",
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "2025-01-01T00:00:00Z",
            Hashtags = [],
            Medias = [new PostMedia { Id = "m-current", Url = "https://cdn/c.jpg", Type = "photo" }],
            Deleted = deleted,
            Changes = [],
            Index = [],
        };
}
