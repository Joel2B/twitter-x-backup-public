using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Backup.App.Utils;

public static class PostHash
{
    public static string Compute(Models.Post.Post post)
    {
        object normalized = Normalize(post);
        string json = JsonConvert.SerializeObject(normalized);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        byte[] hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    private static object Normalize(Models.Post.Post post)
    {
        Models.Post.Profile profile = post.Profile;

        List<object>? medias = post
            .Medias?.Select(media => new
            {
                media.Id,
                media.Url,
                media.Type,
                media.VideoInfo?.DurationMilis,
                Variants = media
                    .VideoInfo?.Variants?.Select(variant => new
                    {
                        variant.ContentType,
                        variant.Bitrate,
                        variant.Url,
                    })
                    .OrderBy(variant => variant.ContentType, StringComparer.Ordinal)
                    .ThenBy(variant => variant.Bitrate)
                    .ThenBy(variant => variant.Url, StringComparer.Ordinal)
                    .ToList(),
            })
            .OrderBy(media => media.Id, StringComparer.Ordinal)
            .ThenBy(media => media.Url, StringComparer.Ordinal)
            .ThenBy(media => media.Type, StringComparer.Ordinal)
            .Cast<object>()
            .ToList();

        List<object> index = post
            .Index.OrderBy(o => o.Key, StringComparer.Ordinal)
            .SelectMany(user =>
                user.Value.OrderBy(o => o.Key, StringComparer.Ordinal)
                    .Select(origin => new
                    {
                        UserId = user.Key,
                        Origin = origin.Key,
                        origin.Value.Previous,
                        origin.Value.Next,
                    })
            )
            .Cast<object>()
            .ToList();

        return new
        {
            post.Id,
            Profile = new
            {
                profile.Id,
                profile.UserName,
                profile.Name,
                profile.BannerUrl,
                profile.ImageUrl,
                profile.Following,
                CountMedia = profile.Count?.Media,
            },
            post.Description,
            post.Retweeted,
            post.Favorited,
            post.Bookmarked,
            post.CreatedAt,
            post.Deleted,
            Hashtags = post.Hashtags?.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            Medias = medias,
            Index = index,
        };
    }
}
