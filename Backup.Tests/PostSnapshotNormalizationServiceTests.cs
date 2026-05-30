using Backup.Application.Posts;
using Backup.Domain.Posts;

namespace Backup.Tests;

public class PostSnapshotNormalizationServiceTests
{
    [Fact]
    public void Normalize_KeepsLastById_AndSkipsEmptyIds()
    {
        List<Post> posts =
        [
            CreatePost("p1", "first"),
            CreatePost("p1", "second"),
            CreatePost("p2", "value"),
            CreatePost("", "empty"),
        ];

        PostSnapshotNormalizationService service = new();
        IReadOnlyList<Post> normalized = service.Normalize(posts);

        Assert.Equal(2, normalized.Count);
        Assert.Equal("second", normalized.Single(post => post.Id == "p1").Description);
        Assert.Equal("value", normalized.Single(post => post.Id == "p2").Description);
    }

    [Fact]
    public void Normalize_ReturnsClones()
    {
        Post original = CreatePost("p1", "value");

        PostSnapshotNormalizationService service = new();
        IReadOnlyList<Post> normalized = service.Normalize([original]);

        Post clone = Assert.Single(normalized);
        Assert.NotSame(original, clone);
        Assert.NotSame(original.Profile, clone.Profile);
    }

    private static Post CreatePost(string id, string description) =>
        new()
        {
            Id = id,
            Profile = new PostProfile { Id = "profile-1", UserName = "user" },
            Description = description,
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "2025-01-01T00:00:00Z",
            Hashtags = [],
            Medias = [],
            Deleted = false,
            Changes = [],
            Index = [],
        };
}
