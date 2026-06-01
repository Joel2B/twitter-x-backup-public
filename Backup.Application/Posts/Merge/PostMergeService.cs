using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostMergeService : IPostMergeService
{
    public PostMergeOutcome Merge(
        string userId,
        string origin,
        Post current,
        Post incoming,
        MergeOptions options
    )
    {
        Post merged = incoming.Clone();
        Change change = new() { UserId = userId };

        ApplyProfileFallback(current, merged);

        bool hasDataChange = !current.Equals(merged);
        if (hasDataChange)
            change.Data = current.Clone();

        IndexData? incomingIndexData = null;

        if (options.Index)
        {
            Dictionary<string, IndexData> incomingUserIndex = merged.Index.TryGetValue(
                userId,
                out Dictionary<string, IndexData>? userIndex
            )
                ? userIndex
                : [];

            incomingIndexData = incomingUserIndex.TryGetValue(origin, out IndexData? indexData)
                ? indexData.Clone()
                : new();
        }

        merged.Index = current.CloneIndex();
        merged.Changes = current.CloneChanges();

        bool hasIndexChange = false;
        if (options.Index)
        {
            if (!merged.Index.ContainsKey(userId))
                merged.Index[userId] = [];

            merged.Index[userId][origin] = incomingIndexData ?? new();
            hasIndexChange = TryCaptureIndexChange(current, merged, change, userId, origin);
        }

        if (hasDataChange || hasIndexChange)
            merged.Changes.Add(change);

        return new PostMergeOutcome
        {
            MergedPost = merged,
            HasDataChange = hasDataChange,
            HasIndexChange = hasIndexChange,
            Change = hasDataChange || hasIndexChange ? change : null,
        };
    }

    private static void ApplyProfileFallback(Post current, Post incoming)
    {
        incoming.Profile.UserName ??= current.Profile.UserName;
        incoming.Profile.Name ??= current.Profile.Name;
        incoming.Profile.BannerUrl ??= current.Profile.BannerUrl;
        incoming.Profile.ImageUrl ??= current.Profile.ImageUrl;
        incoming.Profile.Following ??= current.Profile.Following;
    }

    private static bool TryCaptureIndexChange(
        Post current,
        Post merged,
        Change change,
        string userId,
        string origin
    )
    {
        if (!current.Index.TryGetValue(userId, out Dictionary<string, IndexData>? oldUserIndex))
            return false;

        if (!oldUserIndex.TryGetValue(origin, out IndexData? oldIndex))
            return false;

        if (
            !merged.Index.TryGetValue(userId, out Dictionary<string, IndexData>? newUserIndex)
            || !newUserIndex.TryGetValue(origin, out IndexData? newIndex)
        )
            return false;

        if (
            oldIndex.Previous is null
            || oldIndex.Next is null
            || newIndex.Previous is null
            || newIndex.Next is null
            || oldIndex.Equals(newIndex)
        )
            return false;

        change.Index = current.CloneIndex()[userId];
        return true;
    }
}
