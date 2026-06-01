using DomainIndexData = Backup.Domain.Posts.IndexData;
using DomainPost = Backup.Domain.Posts.Post;
using DomainPostMedia = Backup.Domain.Posts.PostMedia;
using DomainPostProfile = Backup.Domain.Posts.PostProfile;
using InfraIndexData = Backup.Infrastructure.Posts.Models.IndexData;
using InfraPost = Backup.Infrastructure.Posts.Models.Post;
using InfraPostMedia = Backup.Infrastructure.Posts.Models.PostMedia;
using InfraPostProfile = Backup.Infrastructure.Posts.Models.PostProfile;

namespace Backup.Tests;

public sealed class PostModelHashCodeTests
{
    [Fact]
    public void Domain_Post_GetHashCode_Does_Not_Throw_And_Is_Consistent_For_Equals()
    {
        DomainPost left = CreateDomainPost();
        DomainPost right = CreateDomainPost();

        int hash = left.GetHashCode();

        Assert.Equal(left, right);
        Assert.Equal(hash, right.GetHashCode());
        Assert.Single(new[] { left, right }.Distinct());
    }

    [Fact]
    public void Infrastructure_Post_GetHashCode_Does_Not_Throw_And_Is_Consistent_For_Equals()
    {
        InfraPost left = CreateInfraPost();
        InfraPost right = CreateInfraPost();

        int hash = left.GetHashCode();

        Assert.Equal(left, right);
        Assert.Equal(hash, right.GetHashCode());
        Assert.Single(new[] { left, right }.Distinct());
    }

    [Fact]
    public void Domain_IndexData_GetHashCode_Is_Consistent_For_Equals()
    {
        DomainIndexData left = new() { Previous = "a", Next = "b" };
        DomainIndexData right = new() { Previous = "a", Next = "b" };

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Infrastructure_IndexData_GetHashCode_Is_Consistent_For_Equals()
    {
        InfraIndexData left = new() { Previous = "a", Next = "b" };
        InfraIndexData right = new() { Previous = "a", Next = "b" };

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    private static DomainPost CreateDomainPost() =>
        new()
        {
            Id = "1",
            Profile = new DomainPostProfile
            {
                Id = "p1",
                UserName = "user",
                Name = "name",
                BannerUrl = "banner",
                ImageUrl = "image",
                Following = true,
            },
            Description = "hello",
            Retweeted = false,
            Favorited = true,
            Bookmarked = false,
            CreatedAt = "now",
            Hashtags = ["b", "a"],
            Medias =
            [
                new DomainPostMedia
                {
                    Id = "m1",
                    Url = "https://cdn/a.jpg",
                    Type = "photo",
                },
            ],
            Deleted = false,
        };

    private static InfraPost CreateInfraPost() =>
        new()
        {
            Id = "1",
            Profile = new InfraPostProfile
            {
                Id = "p1",
                UserName = "user",
                Name = "name",
                BannerUrl = "banner",
                ImageUrl = "image",
                Following = true,
            },
            Description = "hello",
            Retweeted = false,
            Favorited = true,
            Bookmarked = false,
            CreatedAt = "now",
            Hashtags = ["b", "a"],
            Medias =
            [
                new InfraPostMedia
                {
                    Id = "m1",
                    Url = "https://cdn/a.jpg",
                    Type = "photo",
                },
            ],
            Deleted = false,
        };
}
