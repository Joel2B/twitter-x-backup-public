using Backup.App.Models.Data.Json;
using Backup.App.Models.Post;

namespace Backup.App.Data.Post;

public partial class LocalPostData
{
    private static List<Change> ToModelChanges(
        List<PostChangeRow> changeRows,
        Dictionary<(string PostId, int ChangeOrdinal), List<PostChangeFieldRow>> fieldsByChange,
        Models.Post.Post currentPost
    )
    {
        if (changeRows.Count == 0)
            return [];

        List<PostChangeRow> ordered = changeRows
            .OrderBy(o => o.Date)
            .ThenBy(o => o.Ordinal)
            .ToList();

        Models.Post.Data stateData = currentPost.Clone();

        Dictionary<string, Dictionary<string, IndexData>> stateIndex = currentPost.CloneIndex();

        List<Change> result = new(ordered.Count);

        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            PostChangeRow changeRow = ordered[i];
            bool hasDataFields = false;
            bool hasIndexFields = false;

            if (
                !fieldsByChange.TryGetValue(
                    (changeRow.PostId, changeRow.Ordinal),
                    out List<PostChangeFieldRow>? fields
                )
            )
                fields = [];

            foreach (PostChangeFieldRow field in fields)
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
                    UserId = changeRow.UserId,
                    Date = changeRow.Date,
                    Data = hasDataFields ? stateData.Clone() : null,
                    Index = hasIndexFields ? CloneUserIndex(stateIndex, changeRow.UserId) : null,
                }
            );
        }

        result.Reverse();
        return result;
    }

    private static void ApplyReverseDelta(
        Models.Post.Data stateData,
        Dictionary<string, Dictionary<string, IndexData>> stateIndex,
        PostChangeFieldRow field
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

    private static void ApplyOldDataValue(Models.Post.Data data, string field, string? oldValueJson)
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
                    data.Profile.Count = null;

                break;
            }
            case ChangeFields.PostHashtags:
                data.Hashtags = DeserializeJson<List<string>?>(oldValueJson);
                break;
            case ChangeFields.PostMedias:
                data.Medias = DeserializeJson<List<Models.Post.Media>?>(oldValueJson);
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
}
