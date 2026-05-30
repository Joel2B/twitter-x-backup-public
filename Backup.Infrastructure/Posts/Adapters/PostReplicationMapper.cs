using Backup.Domain.Posts;
using AppPosts = Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Posts.Adapters;

internal static class PostReplicationMapper
{
    public static Post ToDomain(AppPosts.Post source) =>
        new()
        {
            Id = source.Id,
            Profile = ToDomain(source.Profile),
            Description = source.Description,
            Retweeted = source.Retweeted,
            Favorited = source.Favorited,
            Bookmarked = source.Bookmarked,
            CreatedAt = source.CreatedAt,
            Hashtags = source.Hashtags is null ? null : [.. source.Hashtags],
            Medias = source.Medias?.Select(ToDomain).ToList(),
            Deleted = source.Deleted,
            Changes = source.Changes.Select(ToDomain).ToList(),
            Index = source.Index.ToDictionary(
                left => left.Key,
                left => left.Value.ToDictionary(right => right.Key, right => ToDomain(right.Value))
            ),
        };

    public static AppPosts.Post ToApp(Post source) =>
        new()
        {
            Id = source.Id,
            Profile = ToApp(source.Profile),
            Description = source.Description,
            Retweeted = source.Retweeted,
            Favorited = source.Favorited,
            Bookmarked = source.Bookmarked,
            CreatedAt = source.CreatedAt,
            Hashtags = source.Hashtags is null ? null : [.. source.Hashtags],
            Medias = source.Medias?.Select(ToApp).ToList(),
            Deleted = source.Deleted,
            Changes = source.Changes.Select(ToApp).ToList(),
            Index = source.Index.ToDictionary(
                left => left.Key,
                left => left.Value.ToDictionary(right => right.Key, right => ToApp(right.Value))
            ),
        };

    public static MediaInput ToDomain(AppPosts.MediaInput source) =>
        new()
        {
            Id = source.Id,
            Profile = ToDomain(source.Profile),
            Medias = source.Medias?.Select(ToDomain).ToList(),
            Deleted = source.Deleted,
        };

    public static AppPosts.MediaInput ToApp(MediaInput source) =>
        new()
        {
            Id = source.Id,
            Profile = ToApp(source.Profile),
            Medias = source.Medias?.Select(ToApp).ToList(),
            Deleted = source.Deleted,
        };

    public static AppPosts.MergeOptions? ToApp(MergeOptions? source) =>
        source is null ? null : new AppPosts.MergeOptions { Index = source.Index };

    public static MergeOptions ToDomain(AppPosts.MergeOptions? source) =>
        source is null ? new MergeOptions() : new MergeOptions { Index = source.Index };

    public static PostStoreCounts ToDomain(AppPosts.PostStoreCounts source) =>
        new()
        {
            Posts = source.Posts,
            Profiles = source.Profiles,
            Hashtags = source.Hashtags,
            Medias = source.Medias,
            MediaVariants = source.MediaVariants,
            IndexEntries = source.IndexEntries,
            Changes = source.Changes,
            ChangeFields = source.ChangeFields,
            HashMeta = source.HashMeta,
        };

    private static PostProfile ToDomain(AppPosts.PostProfile source) =>
        new()
        {
            Id = source.Id,
            UserName = source.UserName,
            Name = source.Name,
            BannerUrl = source.BannerUrl,
            ImageUrl = source.ImageUrl,
            Following = source.Following,
            Count = source.Count is null ? null : new PostCount { Media = source.Count.Media },
        };

    private static AppPosts.PostProfile ToApp(PostProfile source) =>
        new()
        {
            Id = source.Id,
            UserName = source.UserName,
            Name = source.Name,
            BannerUrl = source.BannerUrl,
            ImageUrl = source.ImageUrl,
            Following = source.Following,
            Count = source.Count is null ? null : new AppPosts.PostCount { Media = source.Count.Media },
        };

    private static PostMedia ToDomain(AppPosts.PostMedia source) =>
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
                    Variants = source.VideoInfo.Variants?.Select(ToDomain).ToList(),
                },
        };

    private static AppPosts.PostMedia ToApp(PostMedia source) =>
        new()
        {
            Id = source.Id,
            Url = source.Url,
            Type = source.Type,
            VideoInfo = source.VideoInfo is null
                ? null
                : new AppPosts.PostVideoInfo
                {
                    DurationMilis = source.VideoInfo.DurationMilis,
                    Variants = source.VideoInfo.Variants?.Select(ToApp).ToList(),
                },
        };

    private static PostVariant ToDomain(AppPosts.PostVariant source) =>
        new() { ContentType = source.ContentType, Bitrate = source.Bitrate, Url = source.Url };

    private static AppPosts.PostVariant ToApp(PostVariant source) =>
        new() { ContentType = source.ContentType, Bitrate = source.Bitrate, Url = source.Url };

    private static Change ToDomain(AppPosts.Change source) =>
        new()
        {
            UserId = source.UserId,
            Date = source.Date,
            Data = source.Data is null ? null : ToDomain(source.Data),
            Index = source.Index?.ToDictionary(entry => entry.Key, entry => ToDomain(entry.Value)),
        };

    private static AppPosts.Change ToApp(Change source) =>
        new()
        {
            UserId = source.UserId,
            Date = source.Date,
            Data = source.Data is null ? null : ToApp(source.Data),
            Index = source.Index?.ToDictionary(entry => entry.Key, entry => ToApp(entry.Value)),
        };

    private static PostData ToDomain(AppPosts.PostData source) =>
        new()
        {
            Id = source.Id,
            Profile = ToDomain(source.Profile),
            Description = source.Description,
            Retweeted = source.Retweeted,
            Favorited = source.Favorited,
            Bookmarked = source.Bookmarked,
            CreatedAt = source.CreatedAt,
            Hashtags = source.Hashtags is null ? null : [.. source.Hashtags],
            Medias = source.Medias?.Select(ToDomain).ToList(),
            Deleted = source.Deleted,
        };

    private static AppPosts.PostData ToApp(PostData source) =>
        new()
        {
            Id = source.Id,
            Profile = ToApp(source.Profile),
            Description = source.Description,
            Retweeted = source.Retweeted,
            Favorited = source.Favorited,
            Bookmarked = source.Bookmarked,
            CreatedAt = source.CreatedAt,
            Hashtags = source.Hashtags is null ? null : [.. source.Hashtags],
            Medias = source.Medias?.Select(ToApp).ToList(),
            Deleted = source.Deleted,
        };

    private static IndexData ToDomain(AppPosts.IndexData source) =>
        new() { Previous = source.Previous, Next = source.Next };

    private static AppPosts.IndexData ToApp(IndexData source) =>
        new() { Previous = source.Previous, Next = source.Next };
}
