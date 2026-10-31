using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostHashingService(IPostChangeComputationService postChangeComputationService)
    : IPostHashingService
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private readonly IPostChangeComputationService _postChangeComputationService =
        postChangeComputationService;

    public string Compute(Post post)
    {
        object normalized = Normalize(post, _postChangeComputationService.Compute(post));
        string json = JsonSerializer.Serialize(normalized, JsonOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        byte[] hash = SHA256.HashData(bytes);

        return $"v2:{Convert.ToHexString(hash)}";
    }

    private static object Normalize(Post post, IReadOnlyList<PostComputedChange> computedChanges)
    {
        List<object> changes = computedChanges
            .Select(
                (change, index) =>
                    new
                    {
                        Sequence = index,
                        change.UserId,
                        Date = NormalizeDate(change.Date),
                        change.ChangeType,
                        Fields = change.Fields.Select(field => new
                        {
                            field.Field,
                            field.OldValueJson,
                            field.NewValueJson,
                        }),
                    }
            )
            .OrderBy(change => change.Date, StringComparer.Ordinal)
            .ThenBy(change => change.Sequence)
            .Select(change => new
            {
                change.UserId,
                change.Date,
                change.ChangeType,
                change.Fields,
            })
            .Cast<object>()
            .ToList();

        return new
        {
            Data = NormalizeData(post),
            Changes = changes,
            Index = NormalizeCurrentIndex(post.Index),
        };
    }

    private static object NormalizeData(PostData post)
    {
        PostProfile profile = post.Profile;

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
        };
    }

    private static List<object> NormalizeCurrentIndex(
        IReadOnlyDictionary<string, Dictionary<string, IndexData>> index
    ) =>
        index
            .OrderBy(user => user.Key, StringComparer.Ordinal)
            .SelectMany(user =>
                user.Value.OrderBy(origin => origin.Key, StringComparer.Ordinal)
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

    private static string NormalizeDate(DateTime date)
    {
        DateTime utc = date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc),
        };

        return utc.ToString("O", CultureInfo.InvariantCulture);
    }
}
