using Backup.Application.Posts;
using Backup.Domain.Posts;

namespace Backup.Tests;

public class PostHashingServiceTests
{
    private readonly PostHashingService _sut = new();

    [Fact]
    public void Compute_IsStable_ForEquivalentDataWithDifferentOrdering()
    {
        Post postA = BuildSamplePost();
        Post postB = BuildSamplePost();

        postB.Hashtags = ["zeta", "alpha"];
        postB.Medias =
        [
            postB.Medias![1],
            postB.Medias[0],
        ];
        postB.Medias[0].VideoInfo!.Variants =
        [
            postB.Medias[0].VideoInfo!.Variants![1],
            postB.Medias[0].VideoInfo!.Variants![0],
        ];

        string hashA = _sut.Compute(postA);
        string hashB = _sut.Compute(postB);

        Assert.Equal(hashA, hashB);
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
