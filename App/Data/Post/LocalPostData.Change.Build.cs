using Backup.App.Models.Data.Json;
using Backup.App.Models.Post;

namespace Backup.App.Data.Post;

public partial class LocalPostData
{
    private static void AddChangeRows(LocalPostTables tables, Models.Post.Post post)
    {
        if (post.Changes.Count == 0)
            return;

        List<Change> orderedChanges = post
            .Changes.Select((change, index) => new { Change = change, Index = index })
            .OrderBy(o => o.Change.Date)
            .ThenBy(o => o.Index)
            .Select(o => o.Change)
            .ToList();

        int ordinal = 0;

        for (int i = 0; i < orderedChanges.Count; i++)
        {
            Change change = orderedChanges[i];
            List<PostChangeFieldRow> fields = BuildChangeFields(post, orderedChanges, i);

            if (fields.Count == 0)
                continue;

            tables.PostChanges.Add(
                new()
                {
                    PostId = post.Id,
                    Ordinal = ordinal,
                    UserId = change.UserId,
                    Date = change.Date,
                    ChangeType = GetChangeType(fields),
                }
            );

            for (int j = 0; j < fields.Count; j++)
            {
                PostChangeFieldRow field = fields[j];
                field.PostId = post.Id;
                field.ChangeOrdinal = ordinal;
                field.Ordinal = j;

                tables.PostChangeFields.Add(field);
            }

            ordinal++;
        }
    }

    private static List<PostChangeFieldRow> BuildChangeFields(
        Models.Post.Post post,
        List<Change> changes,
        int index
    )
    {
        Change change = changes[index];
        List<PostChangeFieldRow> fields = [];

        if (change.Data is not null)
        {
            Models.Post.Data newData = GetNextDataState(post, changes, index);
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

    private static Models.Post.Data GetNextDataState(
        Models.Post.Post post,
        List<Change> changes,
        int index
    )
    {
        for (int i = index + 1; i < changes.Count; i++)
            if (changes[i].Data is not null)
                return changes[i].Data!.Clone();

        return post.Clone();
    }

    private static Dictionary<string, IndexData>? GetNextIndexState(
        Models.Post.Post post,
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
        List<PostChangeFieldRow> fields,
        Models.Post.Data oldData,
        Models.Post.Data newData
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
        List<PostChangeFieldRow> fields,
        string field,
        T oldValue,
        T newValue
    )
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
            return;

        fields.Add(
            new()
            {
                Field = field,
                OldValueJson = SerializeJson(oldValue),
                NewValueJson = SerializeJson(newValue),
            }
        );
    }

    private static void AddFieldIfDifferentAsJson(
        List<PostChangeFieldRow> fields,
        string field,
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
                Field = field,
                OldValueJson = oldJson,
                NewValueJson = newJson,
            }
        );
    }

    private static string GetChangeType(List<PostChangeFieldRow> fields)
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
}
