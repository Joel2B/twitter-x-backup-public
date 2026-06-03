using System.Text.Json;
using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostChangeReadModelProjectionService : IPostChangeReadModelProjectionService
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public IReadOnlyList<Change> Project(
        Post currentPost,
        IReadOnlyList<PostChangeReplayEntry> entries
    )
    {
        if (entries.Count == 0)
            return [];

        List<PostChangeReplayEntry> ordered = entries
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Sequence)
            .ToList();

        PostData stateData = currentPost.Clone();
        Dictionary<string, Dictionary<string, IndexData>> stateIndex = currentPost.CloneIndex();
        List<Change> result = new(ordered.Count);

        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            PostChangeReplayEntry entry = ordered[i];
            bool hasDataFields = false;
            bool hasIndexFields = false;

            foreach (PostChangeReplayField field in entry.Fields)
            {
                if (IsPostOrProfileField(field.Field))
                    hasDataFields = true;

                if (IsIndexField(field.Field))
                    hasIndexFields = true;

                ApplyReverseDelta(stateData, stateIndex, field);
            }

            result.Add(
                new Change
                {
                    UserId = entry.UserId,
                    Date = entry.Date,
                    Data = hasDataFields ? stateData.Clone() : null,
                    Index = hasIndexFields ? CloneUserIndex(stateIndex, entry.UserId) : null,
                }
            );
        }

        result.Reverse();
        return result;
    }

    private static void ApplyReverseDelta(
        PostData stateData,
        Dictionary<string, Dictionary<string, IndexData>> stateIndex,
        PostChangeReplayField field
    )
    {
        if (IsPostOrProfileField(field.Field))
        {
            ApplyOldDataValue(stateData, field.Field, field.OldValueJson);
            return;
        }

        string? userId = GetIndexUserId(field.Field);
        if (userId is null)
            return;

        Dictionary<string, IndexData>? oldIndex = DeserializeJson<Dictionary<string, IndexData>?>(
            field.OldValueJson
        );

        if (oldIndex is null)
        {
            stateIndex.Remove(userId);
            return;
        }

        stateIndex[userId] = CloneIndex(oldIndex);
    }

    private static void ApplyOldDataValue(PostData data, string field, string? oldValueJson)
    {
        switch (field)
        {
            case PostId:
                data.Id = DeserializeJson<string>(oldValueJson) ?? data.Id;
                break;
            case PostDescription:
                data.Description = DeserializeJson<string>(oldValueJson) ?? data.Description;
                break;
            case PostRetweeted:
                data.Retweeted = DeserializeJson<bool>(oldValueJson);
                break;
            case PostFavorited:
                data.Favorited = DeserializeJson<bool>(oldValueJson);
                break;
            case PostBookmarked:
                data.Bookmarked = DeserializeJson<bool>(oldValueJson);
                break;
            case PostCreatedAt:
                data.CreatedAt = DeserializeJson<string>(oldValueJson) ?? data.CreatedAt;
                break;
            case PostDeleted:
                data.Deleted = DeserializeJson<bool>(oldValueJson);
                break;
            case ProfileId:
                data.Profile.Id = DeserializeJson<string>(oldValueJson) ?? data.Profile.Id;
                break;
            case ProfileUserName:
                data.Profile.UserName = DeserializeJson<string?>(oldValueJson);
                break;
            case ProfileName:
                data.Profile.Name = DeserializeJson<string?>(oldValueJson);
                break;
            case ProfileBannerUrl:
                data.Profile.BannerUrl = DeserializeJson<string?>(oldValueJson);
                break;
            case ProfileImageUrl:
                data.Profile.ImageUrl = DeserializeJson<string?>(oldValueJson);
                break;
            case ProfileFollowing:
                data.Profile.Following = DeserializeJson<bool?>(oldValueJson);
                break;
            case ProfileCountMedia:
            {
                int? mediaCount = DeserializeJson<int?>(oldValueJson);
                if (mediaCount.HasValue)
                {
                    data.Profile.Count ??= new();
                    data.Profile.Count.Media = mediaCount;
                }
                else
                {
                    data.Profile.Count = null;
                }

                break;
            }
            case PostHashtags:
                data.Hashtags = DeserializeJson<List<string>?>(oldValueJson);
                break;
            case PostMedias:
                data.Medias = DeserializeJson<List<PostMedia>?>(oldValueJson);
                break;
        }
    }

    private static Dictionary<string, IndexData>? CloneUserIndex(
        Dictionary<string, Dictionary<string, IndexData>> index,
        string userId
    )
    {
        if (!index.TryGetValue(userId, out Dictionary<string, IndexData>? userIndex))
            return null;

        return CloneIndex(userIndex);
    }

    private static Dictionary<string, IndexData> CloneIndex(Dictionary<string, IndexData> index) =>
        index.ToDictionary(item => item.Key, item => item.Value.Clone(), StringComparer.Ordinal);

    private static T? DeserializeJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private const string PostId = "post.id";
    private const string PostDescription = "post.description";
    private const string PostRetweeted = "post.retweeted";
    private const string PostFavorited = "post.favorited";
    private const string PostBookmarked = "post.bookmarked";
    private const string PostCreatedAt = "post.createdAt";
    private const string PostDeleted = "post.deleted";
    private const string ProfileId = "profile.id";
    private const string ProfileUserName = "profile.userName";
    private const string ProfileName = "profile.name";
    private const string ProfileBannerUrl = "profile.bannerUrl";
    private const string ProfileImageUrl = "profile.imageUrl";
    private const string ProfileFollowing = "profile.following";
    private const string ProfileCountMedia = "profile.count.media";
    private const string PostHashtags = "post.hashtags";
    private const string PostMedias = "post.medias";

    private static bool IsPostOrProfileField(string field) =>
        field.StartsWith("post.", StringComparison.Ordinal)
        || field.StartsWith("profile.", StringComparison.Ordinal);

    private static bool IsIndexField(string field) =>
        field.StartsWith("index.", StringComparison.Ordinal);

    private static string? GetIndexUserId(string field)
    {
        if (!IsIndexField(field))
            return null;

        string[] parts = field.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : null;
    }
}
