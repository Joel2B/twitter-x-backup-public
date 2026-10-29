using Backup.Application.Posts;
using Backup.Domain.Posts;

namespace Backup.Tests;

public class PostHashingServiceTests
{
    private readonly PostHashingService _sut = new(new PostChangeComputationService());

    [Fact]
    public void Compute_IsStable_ForEquivalentDataWithDifferentOrdering()
    {
        Post postA = BuildSamplePost();
        Post postB = BuildSamplePost();

        postB.Hashtags = ["zeta", "alpha"];
        postB.Medias = [postB.Medias![1], postB.Medias[0]];
        postB.Medias[0].VideoInfo!.Variants =
        [
            postB.Medias[0].VideoInfo!.Variants![1],
            postB.Medias[0].VideoInfo!.Variants![0],
        ];

        string hashA = _sut.Compute(postA);
        string hashB = _sut.Compute(postB);

        Assert.Equal(hashA, hashB);
        Assert.StartsWith("v2:", hashA);
    }

    [Fact]
    public void Compute_Changes_WhenMeaningfulFieldChanges()
    {
        Post postA = BuildSamplePost();
        Post postB = BuildSamplePost();
        postB.Description = "changed";

        string hashA = _sut.Compute(postA);
        string hashB = _sut.Compute(postB);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void Compute_Changes_WhenHistoryChanges()
    {
        Post postA = BuildSamplePost();
        Post postB = BuildSamplePost();
        PostData oldData = postA.Clone();
        oldData.Description = "old";
        Change change = new()
        {
            UserId = "u1",
            Date = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Data = oldData,
            Index = new Dictionary<string, IndexData>(StringComparer.Ordinal)
            {
                ["likes"] = new() { Previous = "before", Next = "after" },
            },
        };
        postA.Changes.Add(change);
        postB.Changes.Add(change.Clone());
        postB.Changes[0].Data!.Description = "different-old-value";

        Assert.NotEqual(_sut.Compute(postA), _sut.Compute(postB));
    }

    [Fact]
    public void Compute_IgnoresHistoryWithoutPersistedFields()
    {
        Post postA = BuildSamplePost();
        Post postB = BuildSamplePost();
        postB.Changes.Add(
            new Change
            {
                UserId = "u1",
                Date = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                Data = postB.Clone(),
            }
        );

        Assert.Equal(_sut.Compute(postA), _sut.Compute(postB));
    }

    [Fact]
    public void Compute_IsStable_ForEquivalentHistoryCollectionOrdering()
    {
        Post postA = BuildSamplePost();
        PostData oldData = postA.Clone();
        postA.Changes =
        [
            new Change
            {
                UserId = "u1",
                Date = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                Data = oldData,
                Index = new Dictionary<string, IndexData>(StringComparer.Ordinal)
                {
                    ["posts"] = new() { Previous = "p", Next = "n" },
                    ["likes"] = new() { Previous = "lp", Next = "ln" },
                },
            },
        ];
        Post postB = postA.Clone();
        postB.Changes[0].Index = postB
            .Changes[0]
            .Index!
            .Reverse()
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        Assert.Equal(_sut.Compute(postA), _sut.Compute(postB));
    }

    [Fact]
    public void Compute_IsStable_ForHistoryWithDifferentDateOrdering()
    {
        Post postA = BuildSamplePost();
        PostData first = postA.Clone();
        first.Description = "first";
        PostData second = postA.Clone();
        second.Description = "second";
        postA.Changes =
        [
            new Change
            {
                UserId = "u1",
                Date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Data = first,
            },
            new Change
            {
                UserId = "u1",
                Date = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                Data = second,
            },
        ];
        Post postB = postA.Clone();
        postB.Changes.Reverse();

        Assert.Equal(_sut.Compute(postA), _sut.Compute(postB));
    }

    private static Post BuildSamplePost() =>
        new()
        {
            Id = "1",
            Profile = new PostProfile
            {
                Id = "u1",
                UserName = "alice",
                Name = "Alice",
                BannerUrl = "https://x.com/banner.jpg",
                ImageUrl = "https://x.com/profile.jpg",
                Following = true,
                Count = new PostCount { Media = 10 },
            },
            Description = "hello",
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "Sun May 24 08:40:16 +0000 2026",
            Hashtags = ["alpha", "zeta"],
            Medias =
            [
                new PostMedia
                {
                    Id = "m2",
                    Url = "https://x.com/2.jpg",
                    Type = "photo",
                },
                new PostMedia
                {
                    Id = "m1",
                    Url = "https://x.com/1.mp4",
                    Type = "video",
                    VideoInfo = new PostVideoInfo
                    {
                        DurationMilis = 1000,
                        Variants =
                        [
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 2048,
                                Url = "https://x.com/1-720.mp4",
                            },
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 1024,
                                Url = "https://x.com/1-480.mp4",
                            },
                        ],
                    },
                },
            ],
            Deleted = false,
            Index = new Dictionary<string, Dictionary<string, IndexData>>(StringComparer.Ordinal)
            {
                ["u1"] = new Dictionary<string, IndexData>(StringComparer.Ordinal)
                {
                    ["notifications"] = new() { Previous = "p1", Next = "n1" },
                },
            },
        };
}
