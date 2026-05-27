using Backup.App.Api.Models;
using Backup.App.Models.Posts;

namespace Backup.App.Api.Services;

public static class ProcessedPostMapper
{
    public static List<Post> MapMany(IReadOnlyCollection<ProcessedPostInput> inputs) =>
        [.. inputs.Select(Map)];

    public static Post Map(ProcessedPostInput input)
    {
        return new Post
        {
            Id = input.Id.Trim(),
            Profile = new PostProfile
            {
                Id = input.Profile.Id.Trim(),
                UserName = input.Profile.UserName,
                Name = input.Profile.Name,
                BannerUrl = input.Profile.BannerUrl,
                ImageUrl = input.Profile.ImageUrl,
                Following = input.Profile.Following,
                Count = null,
            },
            Description = input.Description,
            Retweeted = input.Retweeted ?? false,
            Favorited = input.Favorited ?? false,
            Bookmarked = input.Bookmarked ?? false,
            CreatedAt = input.CreatedAt,
            Hashtags = MapHashtags(input.Hashtags),
            Medias = MapMedias(input.Medias),
            Deleted = input.Deleted ?? false,
        };
    }

    private static List<string>? MapHashtags(List<string>? hashtags)
    {
        List<string>? mapped = hashtags
            ?.Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return mapped is { Count: > 0 } ? mapped : null;
    }

    private static List<PostMedia>? MapMedias(List<ProcessedPostMediaInput>? medias)
    {
        List<PostMedia>? mapped = medias
            ?.Select(media => new PostMedia
            {
                Id = media.Id,
                Url = media.Url,
                Type = media.Type,
                VideoInfo = MapVideoInfo(media.VideoInfo),
            })
            .ToList();

        return mapped is { Count: > 0 } ? mapped : null;
    }

    private static PostVideoInfo? MapVideoInfo(ProcessedPostVideoInfoInput? videoInfo)
    {
        if (videoInfo is null)
            return null;

        return new PostVideoInfo
        {
            DurationMilis = videoInfo.DurationMilis,
            Variants = videoInfo.Variants?.Select(MapVariant).ToList(),
        };
    }

    private static PostVariant MapVariant(ProcessedPostVariantInput variant) =>
        new()
        {
            ContentType = variant.ContentType,
            Bitrate = variant.Bitrate,
            Url = variant.Url,
        };
}
