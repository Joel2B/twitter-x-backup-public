using DomainPost = Backup.Domain.Posts.Post;
using DomainPostMedia = Backup.Domain.Posts.PostMedia;
using DomainPostProfile = Backup.Domain.Posts.PostProfile;
using InfrastructurePost = Backup.Infrastructure.Posts.Models.Stored.Post;
using InfrastructurePostMedia = Backup.Infrastructure.Posts.Models.Stored.PostMedia;
using InfrastructurePostProfile = Backup.Infrastructure.Posts.Models.Stored.PostProfile;

namespace Backup.Tests;

public class PostEqualityHashCodeTests
{
    [Fact]
    public void DomainPost_EqualPosts_HaveSameHash_AndDistinctWorks()
    {
        DomainPost first = CreateDomainPost("1", "hello", "https://cdn/1.jpg");
        DomainPost second = CreateDomainPost("2", "hello", "https://cdn/1.jpg");
        DomainPost third = CreateDomainPost("3", "other", "https://cdn/3.jpg");

        int hash1 = first.GetHashCode();
        int hash2 = second.GetHashCode();

        Assert.Equal(hash1, hash2);
        Assert.True(first.Equals(second));
        Assert.False(first.Equals(third));

        List<DomainPost> distinct = [first, second, third];
        Assert.Equal(2, distinct.Distinct().Count());
    }

    [Fact]
    public void InfrastructurePost_EqualPosts_HaveSameHash_AndDistinctWorks()
    {
        InfrastructurePost first = CreateInfrastructurePost("1", "hello", "https://cdn/1.jpg");
        InfrastructurePost second = CreateInfrastructurePost("2", "hello", "https://cdn/1.jpg");
        InfrastructurePost third = CreateInfrastructurePost("3", "other", "https://cdn/3.jpg");

        int hash1 = first.GetHashCode();
        int hash2 = second.GetHashCode();

        Assert.Equal(hash1, hash2);
        Assert.True(first.Equals(second));
        Assert.False(first.Equals(third));

        List<InfrastructurePost> distinct = [first, second, third];
        Assert.Equal(2, distinct.Distinct().Count());
    }

    private static DomainPost CreateDomainPost(string id, string description, string mediaUrl) =>
        new()
        {
            Id = id,
            Profile = new DomainPostProfile
            {
                Id = "profile-1",
                UserName = "user-1",
                Name = "name-1",
                BannerUrl = "https://cdn/banner.jpg",
                ImageUrl = "https://cdn/image.jpg",
                Following = true,
            },
            Description = description,
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "2026-01-01T00:00:00Z",
            Hashtags = ["tag-a", "tag-b"],
            Medias =
            [
                new DomainPostMedia
                {
                    Id = "media-1",
                    Url = mediaUrl,
                    Type = "photo",
                },
            ],
        };

    private static InfrastructurePost CreateInfrastructurePost(
        string id,
        string description,
        string mediaUrl
    ) =>
        new()
        {
            Id = id,
            Profile = new InfrastructurePostProfile
            {
                Id = "profile-1",
                UserName = "user-1",
                Name = "name-1",
                BannerUrl = "https://cdn/banner.jpg",
                ImageUrl = "https://cdn/image.jpg",
                Following = true,
            },
            Description = description,
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "2026-01-01T00:00:00Z",
            Hashtags = ["tag-a", "tag-b"],
            Medias =
            [
                new InfrastructurePostMedia
                {
                    Id = "media-1",
                    Url = mediaUrl,
                    Type = "photo",
                },
            ],
        };
}
