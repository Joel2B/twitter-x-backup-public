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
                if (ChangeFields.IsPostOrProfile(field.Field))
                    hasDataFields = true;

                if (ChangeFields.IsIndex(field.Field))
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
        if (ChangeFields.IsPostOrProfile(field.Field))
        {
            ApplyOldDataValue(stateData, field.Field, field.OldValueJson);
            return;
        }

        string? userId = ChangeFields.GetIndexUserId(field.Field);
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
            case ChangeFields.PostId:
                data.Id = DeserializeJson<string>(oldValueJson) ?? data.Id;
                break;
            case ChangeFields.PostDescription:
                data.Description = DeserializeJson<string>(oldValueJson) ?? data.Description;
                break;
            case ChangeFields.PostRetweeted:
                data.Retweeted = DeserializeJson<bool>(oldValueJson);
                break;
            case ChangeFields.PostFavorited:
                data.Favorited = DeserializeJson<bool>(oldValueJson);
                break;
            case ChangeFields.PostBookmarked:
                data.Bookmarked = DeserializeJson<bool>(oldValueJson);
                break;
            case ChangeFields.PostCreatedAt:
                data.CreatedAt = DeserializeJson<string>(oldValueJson) ?? data.CreatedAt;
                break;
            case ChangeFields.PostDeleted:
                data.Deleted = DeserializeJson<bool>(oldValueJson);
                break;
            case ChangeFields.ProfileId:
                data.Profile.Id = DeserializeJson<string>(oldValueJson) ?? data.Profile.Id;
                break;
            case ChangeFields.ProfileUserName:
                data.Profile.UserName = DeserializeJson<string?>(oldValueJson);
                break;
            case ChangeFields.ProfileName:
                data.Profile.Name = DeserializeJson<string?>(oldValueJson);
                break;
            case ChangeFields.ProfileBannerUrl:
                data.Profile.BannerUrl = DeserializeJson<string?>(oldValueJson);
                break;
            case ChangeFields.ProfileImageUrl:
                data.Profile.ImageUrl = DeserializeJson<string?>(oldValueJson);
                break;
            case ChangeFields.ProfileFollowing:
                data.Profile.Following = DeserializeJson<bool?>(oldValueJson);
                break;
            case ChangeFields.ProfileCountMedia:
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
            case ChangeFields.PostHashtags:
                data.Hashtags = DeserializeJson<List<string>?>(oldValueJson);
                break;
            case ChangeFields.PostMedias:
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

}
