using System.Text.Json;
using Backup.App.Models.Posts;

namespace Backup.App.Data.Posts;

public partial class SqlitePostData
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private static List<PostChangeFieldEntity> BuildChangeFields(
        Post post,
        List<Change> changes,
        int index
    )
    {
        Change change = changes[index];
        List<PostChangeFieldEntity> fields = new();

        if (change.Data is not null)
        {
            PostData newData = GetNextDataState(post, changes, index);
            AddDataFieldChanges(fields, change.Data, newData);
        }

        if (change.Index is not null)
        {
            Dictionary<string, IndexData>? newIndex = GetNextIndexState(
                post,
                changes,
                index,
                change.UserId
            );

            AddFieldIfDifferentAsJson(
                fields,
                ChangeFields.Index(change.UserId),
                NormalizeIndex(change.Index),
                NormalizeIndex(newIndex)
            );
        }

        return fields;
    }

    private static PostData GetNextDataState(Post post, List<Change> changes, int index)
    {
        for (int i = index + 1; i < changes.Count; i++)
        {
            if (changes[i].Data is not null)
                return changes[i].Data!.Clone();
        }

        return post.Clone();
    }

    private static Dictionary<string, IndexData>? GetNextIndexState(
        Post post,
        List<Change> changes,
        int index,
        string userId
    )
    {
        for (int i = index + 1; i < changes.Count; i++)
        {
            Change next = changes[i];

            if (next.UserId == userId && next.Index is not null)
                return CloneIndex(next.Index);
        }

        return post.Index.TryGetValue(userId, out Dictionary<string, IndexData>? value)
            ? CloneIndex(value)
            : null;
    }

    private static void AddDataFieldChanges(
        List<PostChangeFieldEntity> fields,
        PostData oldData,
        PostData newData
    )
    {
        AddFieldIfDifferent(fields, ChangeFields.PostId, oldData.Id, newData.Id);

        AddFieldIfDifferent(
            fields,
            ChangeFields.PostDescription,
            oldData.Description,
            newData.Description
        );

        AddFieldIfDifferent(
            fields,
            ChangeFields.PostRetweeted,
            oldData.Retweeted,
            newData.Retweeted
        );

        AddFieldIfDifferent(
            fields,
            ChangeFields.PostFavorited,
            oldData.Favorited,
            newData.Favorited
        );

        AddFieldIfDifferent(
            fields,
            ChangeFields.PostBookmarked,
            oldData.Bookmarked,
            newData.Bookmarked
        );

        AddFieldIfDifferent(
            fields,
            ChangeFields.PostCreatedAt,
            oldData.CreatedAt,
            newData.CreatedAt
        );

        AddFieldIfDifferent(fields, ChangeFields.PostDeleted, oldData.Deleted, newData.Deleted);
        AddFieldIfDifferent(fields, ChangeFields.ProfileId, oldData.Profile.Id, newData.Profile.Id);

        AddFieldIfDifferent(
            fields,
            ChangeFields.ProfileUserName,
            oldData.Profile.UserName,
            newData.Profile.UserName
        );

        AddFieldIfDifferent(
            fields,
            ChangeFields.ProfileName,
            oldData.Profile.Name,
            newData.Profile.Name
        );

        AddFieldIfDifferent(
            fields,
            ChangeFields.ProfileBannerUrl,
            oldData.Profile.BannerUrl,
            newData.Profile.BannerUrl
        );

        AddFieldIfDifferent(
            fields,
            ChangeFields.ProfileImageUrl,
            oldData.Profile.ImageUrl,
            newData.Profile.ImageUrl
        );

        AddFieldIfDifferent(
            fields,
            ChangeFields.ProfileFollowing,
            oldData.Profile.Following,
            newData.Profile.Following
        );

        AddFieldIfDifferent(
            fields,
            ChangeFields.ProfileCountMedia,
            oldData.Profile.Count?.Media,
            newData.Profile.Count?.Media
        );

        AddFieldIfDifferentAsJson(
            fields,
            ChangeFields.PostHashtags,
            oldData.Hashtags,
            newData.Hashtags
        );

        AddFieldIfDifferentAsJson(fields, ChangeFields.PostMedias, oldData.Medias, newData.Medias);
    }

    private static void AddFieldIfDifferent<T>(
        List<PostChangeFieldEntity> fields,
        string path,
        T oldValue,
        T newValue
    )
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
            return;

        fields.Add(
            new()
            {
                Field = path,
                OldValueJson = SerializeJson(oldValue),
                NewValueJson = SerializeJson(newValue),
            }
        );
    }

    private static void AddFieldIfDifferentAsJson(
        List<PostChangeFieldEntity> fields,
        string path,
        object? oldValue,
        object? newValue
    )
    {
        string? oldJson = SerializeJson(oldValue);
        string? newJson = SerializeJson(newValue);

        if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
            return;

        fields.Add(
            new()
            {
                Field = path,
                OldValueJson = oldJson,
                NewValueJson = newJson,
            }
        );
    }

    private static string GetChangeType(List<PostChangeFieldEntity> fields)
    {
        bool hasData = fields.Any(o => ChangeFields.IsPostOrProfile(o.Field));
        bool hasIndex = fields.Any(o => ChangeFields.IsIndex(o.Field));

        if (hasData && hasIndex)
            return "data_index_update";

        if (hasData)
            return "data_update";

        if (hasIndex)
            return "index_update";

        return "unknown";
    }

    private static string? SerializeJson(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static T? DeserializeJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static List<Change> ToModelChanges(List<PostChangeEntity> entities, Post currentPost)
    {
        if (entities.Count == 0)
            return [];

        List<PostChangeEntity> ordered = entities.OrderBy(o => o.Date).ThenBy(o => o.Id).ToList();
        PostData stateData = currentPost.Clone();
        Dictionary<string, Dictionary<string, IndexData>> stateIndex = currentPost.CloneIndex();
        List<Change> result = new(ordered.Count);

        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            PostChangeEntity entity = ordered[i];
            bool hasDataFields = false;
            bool hasIndexFields = false;

            foreach (PostChangeFieldEntity changeField in entity.Fields.OrderBy(o => o.Id))
            {
                if (ChangeFields.IsPostOrProfile(changeField.Field))
                    hasDataFields = true;

                if (ChangeFields.IsIndex(changeField.Field))
                    hasIndexFields = true;

                ApplyReverseDelta(stateData, stateIndex, changeField);
            }

            result.Add(
                new Change
                {
                    UserId = entity.UserId,
                    Date = entity.Date,
                    Data = hasDataFields ? stateData.Clone() : null,
                    Index = hasIndexFields ? CloneUserIndex(stateIndex, entity.UserId) : null,
                }
            );
        }

        result.Reverse();
        return result;
    }

    private static void ApplyReverseDelta(
        PostData stateData,
        Dictionary<string, Dictionary<string, IndexData>> stateIndex,
        PostChangeFieldEntity changeField
    )
    {
        if (ChangeFields.IsPostOrProfile(changeField.Field))
        {
            ApplyOldDataValue(stateData, changeField.Field, changeField.OldValueJson);
            return;
        }

        string? userId = ChangeFields.GetIndexUserId(changeField.Field);

        if (userId is null)
            return;

        Dictionary<string, IndexData>? oldIndex = DeserializeJson<Dictionary<string, IndexData>?>(
            changeField.OldValueJson
        );

        if (oldIndex is null)
        {
            stateIndex.Remove(userId);
            return;
        }

        stateIndex[userId] = CloneIndex(oldIndex);
    }

    private static void ApplyOldDataValue(PostData data, string path, string? oldValueJson)
    {
        switch (path)
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

    private static Dictionary<string, IndexData>? NormalizeIndex(
        Dictionary<string, IndexData>? index
    ) =>
        index
            ?.OrderBy(o => o.Key, StringComparer.Ordinal)
            .ToDictionary(o => o.Key, o => o.Value.Clone(), StringComparer.Ordinal);

    private static Dictionary<string, IndexData> CloneIndex(Dictionary<string, IndexData> index) =>
        index.ToDictionary(o => o.Key, o => o.Value.Clone(), StringComparer.Ordinal);
}
