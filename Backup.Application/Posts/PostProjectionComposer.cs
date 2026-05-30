using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public class PostProjectionComposer : IPostProjectionComposer
{
    public Post Compose(ParsedPostProjection source) =>
        new()
        {
            Id = source.Id,
            Profile = new PostProfile
            {
                Id = source.Profile.Id,
                UserName = source.Profile.UserName,
                Name = source.Profile.Name,
                BannerUrl = source.Profile.BannerUrl,
                ImageUrl = source.Profile.ImageUrl,
                Following = source.Profile.Following,
                Count = source.Profile.MediaCount is null
                    ? null
                    : new PostCount { Media = source.Profile.MediaCount },
            },
            Description = source.Description,
            Retweeted = source.Retweeted,
            Favorited = source.Favorited,
            Bookmarked = source.Bookmarked,
            CreatedAt = source.CreatedAt,
            Hashtags = source.Hashtags is null ? null : [.. source.Hashtags],
            Medias = source.Medias?.Select(ComposeMedia).ToList(),
        };

    public List<Post> ComposeMany(IEnumerable<ParsedPostProjection> source) =>
        source.Select(Compose).ToList();

    private static PostMedia ComposeMedia(ParsedPostMediaProjection source) =>
        new()
        {
            Id = source.Id,
            Url = source.Url,
            Type = source.Type,
            VideoInfo = source.VideoInfo is null
                ? null
                : new PostVideoInfo
                {
                    DurationMilis = source.VideoInfo.DurationMilis,
                    Variants = source.VideoInfo.Variants?.Select(ComposeVariant).ToList(),
                },
        };

    private static PostVariant ComposeVariant(ParsedPostVariantProjection source) =>
        new() { ContentType = source.ContentType, Bitrate = source.Bitrate, Url = source.Url };
}
