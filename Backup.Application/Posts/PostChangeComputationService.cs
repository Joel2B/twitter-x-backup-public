using System.Text.Json;
using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostChangeComputationService : IPostChangeComputationService
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private const string PostPrefix = "post.";
    private const string ProfilePrefix = "profile.";
    private const string IndexPrefix = "index.";

    private const string PostId = "post.id";
    private const string PostDescription = "post.description";
    private const string PostRetweeted = "post.retweeted";
    private const string PostFavorited = "post.favorited";
    private const string PostBookmarked = "post.bookmarked";
    private const string PostCreatedAt = "post.created_at";
    private const string PostDeleted = "post.deleted";
    private const string PostHashtags = "post.hashtags";
    private const string PostMedias = "post.medias";
    private const string ProfileId = "profile.id";
    private const string ProfileUserName = "profile.user_name";
    private const string ProfileName = "profile.name";
    private const string ProfileBannerUrl = "profile.banner_url";
    private const string ProfileImageUrl = "profile.image_url";
    private const string ProfileFollowing = "profile.following";
    private const string ProfileCountMedia = "profile.count_media";

    public IReadOnlyList<PostComputedChange> Compute(Post post)
    {
        if (post.Changes.Count == 0)
            return [];

        List<Change> orderedChanges = post
            .Changes.Select((change, index) => new { Change = change, Index = index })
            .OrderBy(o => o.Change.Date)
            .ThenBy(o => o.Index)
            .Select(o => o.Change)
            .ToList();

        List<PostComputedChange> computed = [];

        for (int index = 0; index < orderedChanges.Count; index++)
        {
            Change change = orderedChanges[index];
            List<PostComputedChangeField> fields = BuildChangeFields(post, orderedChanges, index);

            if (fields.Count == 0)
                continue;

            computed.Add(
                new PostComputedChange
                {
                    UserId = change.UserId,
                    Date = change.Date,
                    ChangeType = GetChangeType(fields),
                    Fields = fields,
                }
            );
        }

        return computed;
    }

    private static List<PostComputedChangeField> BuildChangeFields(
        Post post,
        List<Change> changes,
        int index
    )
    {
        Change change = changes[index];
        List<PostComputedChangeField> fields = [];

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
                Index(change.UserId),
                NormalizeIndex(change.Index),
                NormalizeIndex(newIndex)
            );
        }

        return fields;
    }

    private static PostData GetNextDataState(Post post, List<Change> changes, int index)
    {
        for (int i = index + 1; i < changes.Count; i++)
            if (changes[i].Data is not null)
                return changes[i].Data!.Clone();

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
        List<PostComputedChangeField> fields,
        PostData oldData,
        PostData newData
    )
    {
        AddFieldIfDifferent(fields, PostId, oldData.Id, newData.Id);
        AddFieldIfDifferent(fields, PostDescription, oldData.Description, newData.Description);
        AddFieldIfDifferent(fields, PostRetweeted, oldData.Retweeted, newData.Retweeted);
        AddFieldIfDifferent(fields, PostFavorited, oldData.Favorited, newData.Favorited);
        AddFieldIfDifferent(fields, PostBookmarked, oldData.Bookmarked, newData.Bookmarked);
        AddFieldIfDifferent(fields, PostCreatedAt, oldData.CreatedAt, newData.CreatedAt);
        AddFieldIfDifferent(fields, PostDeleted, oldData.Deleted, newData.Deleted);
        AddFieldIfDifferent(fields, ProfileId, oldData.Profile.Id, newData.Profile.Id);
        AddFieldIfDifferent(
            fields,
            ProfileUserName,
            oldData.Profile.UserName,
            newData.Profile.UserName
        );
        AddFieldIfDifferent(fields, ProfileName, oldData.Profile.Name, newData.Profile.Name);
        AddFieldIfDifferent(
            fields,
            ProfileBannerUrl,
            oldData.Profile.BannerUrl,
            newData.Profile.BannerUrl
        );
        AddFieldIfDifferent(
            fields,
            ProfileImageUrl,
            oldData.Profile.ImageUrl,
            newData.Profile.ImageUrl
        );
        AddFieldIfDifferent(
            fields,
            ProfileFollowing,
            oldData.Profile.Following,
            newData.Profile.Following
        );
        AddFieldIfDifferent(
            fields,
            ProfileCountMedia,
            oldData.Profile.Count?.Media,
            newData.Profile.Count?.Media
        );
        AddFieldIfDifferentAsJson(fields, PostHashtags, oldData.Hashtags, newData.Hashtags);
        AddFieldIfDifferentAsJson(fields, PostMedias, oldData.Medias, newData.Medias);
    }

    private static void AddFieldIfDifferent<T>(
        List<PostComputedChangeField> fields,
        string field,
        T oldValue,
        T newValue
    )
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
            return;

        fields.Add(
            new PostComputedChangeField
            {
                Field = field,
                OldValueJson = SerializeJson(oldValue),
                NewValueJson = SerializeJson(newValue),
            }
        );
    }

    private static void AddFieldIfDifferentAsJson(
        List<PostComputedChangeField> fields,
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
            new PostComputedChangeField
            {
                Field = field,
                OldValueJson = oldJson,
                NewValueJson = newJson,
            }
        );
    }

    private static string GetChangeType(IReadOnlyList<PostComputedChangeField> fields)
    {
        bool hasData = fields.Any(o => IsPostOrProfile(o.Field));
        bool hasIndex = fields.Any(o => IsIndex(o.Field));

        if (hasData && hasIndex)
            return "data_index_update";

        if (hasData)
            return "data_update";

        if (hasIndex)
            return "index_update";

        return "unknown";
    }

    private static bool IsPostOrProfile(string field) =>
        field.StartsWith(PostPrefix, StringComparison.Ordinal)
        || field.StartsWith(ProfilePrefix, StringComparison.Ordinal);

    private static bool IsIndex(string field) =>
        field.StartsWith(IndexPrefix, StringComparison.Ordinal);

    private static string Index(string userId) => $"{IndexPrefix}{userId}";

    private static Dictionary<string, IndexData>? NormalizeIndex(
        Dictionary<string, IndexData>? index
    ) =>
        index
            ?.OrderBy(o => o.Key, StringComparer.Ordinal)
            .ToDictionary(o => o.Key, o => o.Value.Clone(), StringComparer.Ordinal);

    private static Dictionary<string, IndexData> CloneIndex(Dictionary<string, IndexData> index) =>
        index.ToDictionary(o => o.Key, o => o.Value.Clone(), StringComparer.Ordinal);

    private static string? SerializeJson(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
}
