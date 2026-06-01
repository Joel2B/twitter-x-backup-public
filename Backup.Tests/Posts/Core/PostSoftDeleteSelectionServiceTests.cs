using Backup.Application.Posts;
using Backup.Domain.Posts;

namespace Backup.Tests;

public class PostSoftDeleteSelectionServiceTests
{
    [Fact]
    public void SelectIds_FiltersByScope_KeepList_AndDeleted()
    {
        List<Post> posts =
        [
            CreatePost("p1", "user-1", "origin-a", deleted: false),
            CreatePost("p2", "user-1", "origin-a", deleted: false),
            CreatePost("p3", "user-1", "origin-b", deleted: false),
            CreatePost("p4", "user-2", "origin-a", deleted: false),
            CreatePost("p5", "user-1", "origin-a", deleted: true),
        ];

        PostSoftDeleteSelectionService service = new();
        IReadOnlyCollection<string> ids = service.SelectIds("user-1", "origin-a", ["p1"], posts);

        Assert.Single(ids);
        Assert.Contains("p2", ids);
    }

    [Fact]
    public void SelectIds_ReturnsDistinctIds()
    {
        List<Post> posts =
        [
            CreatePost("p1", "user-1", "origin-a", deleted: false),
            CreatePost("p1", "user-1", "origin-a", deleted: false),
        ];

        PostSoftDeleteSelectionService service = new();
        IReadOnlyCollection<string> ids = service.SelectIds("user-1", "origin-a", [], posts);

        Assert.Single(ids);
        Assert.Equal("p1", ids.Single());
    }

    private static Post CreatePost(string id, string userId, string origin, bool deleted) =>
        new()
        {
            Id = id,
            Profile = new PostProfile
            {
                Id = "profile-1",
                UserName = "user",
                Name = "name",
            },
            Description = "desc",
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "2025-01-01T00:00:00Z",
            Hashtags = ["tag"],
            Medias = [],
            Deleted = deleted,
            Changes = [],
            Index = new Dictionary<string, Dictionary<string, IndexData>>
            {
                [userId] = new Dictionary<string, IndexData>
                {
                    [origin] = new IndexData { Previous = "p", Next = "n" },
                },
            },
        };
}
