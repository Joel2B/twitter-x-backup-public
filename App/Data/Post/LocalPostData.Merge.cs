using Backup.App.Models.Data.Json;
using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Data.Post;

public partial class LocalPostData
{
    public async Task AddPosts(
        string userId,
        string origin,
        List<Models.Post.Post> incoming,
        MergeOptions? options = null
    )
    {
        options ??= new();

        Dictionary<string, Models.Post.Post> posts = await GetCache() ?? [];
        Dictionary<string, PostMetaRow> postMeta = await GetPostMetaCache();
        _postsCache ??= posts;

        foreach (Models.Post.Post result in incoming)
        {
            if (!posts.TryGetValue(result.Id, out Models.Post.Post? post))
            {
                Models.Post.Post clone = result.Clone();
                posts[result.Id] = clone;
                postMeta[result.Id] = new()
                {
                    Id = result.Id,
                    Hash = ComputePostHash(clone),
                    Deleted = clone.Deleted,
                };
                continue;
            }

            Change change = new() { UserId = userId };
            MergeData(post, result, change);

            IndexData? incomingIndexData = null;

            if (options.Index)
            {
                Dictionary<string, IndexData> incomingUserIndex = result.Index.TryGetValue(
                    userId,
                    out Dictionary<string, IndexData>? userIndex
                )
                    ? userIndex
                    : [];

                incomingIndexData = incomingUserIndex.TryGetValue(origin, out IndexData? indexData)
                    ? indexData.Clone()
                    : new();
            }

            result.Index = post.CloneIndex();
            result.Changes = post.CloneChanges();

            if (options.Index)
            {
                if (!result.Index.ContainsKey(userId))
                    result.Index[userId] = [];

                result.Index[userId][origin] = incomingIndexData ?? new();
                MergeIndex(post, result, change, origin);
            }

            if (change.Data is not null || change.Index is not null)
                result.Changes.Add(change);

            Models.Post.Post merged = result.Clone();
            posts[result.Id] = merged;
            postMeta[result.Id] = new()
            {
                Id = result.Id,
                Hash = ComputePostHash(merged),
                Deleted = merged.Deleted,
            };
        }

        _postMetaCache = postMeta;
    }

    public Task Reset(List<Models.Post.Post> posts)
    {
        _postsCache = posts
            .Where(post => !string.IsNullOrWhiteSpace(post.Id))
            .GroupBy(post => post.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToDictionary(post => post.Id, post => post.Clone(), StringComparer.Ordinal);

        _postMetaCache = _postsCache.ToDictionary(
            entry => entry.Key,
            entry => new PostMetaRow
            {
                Id = entry.Key,
                Hash = ComputePostHash(entry.Value),
                Deleted = entry.Value.Deleted,
            },
            StringComparer.Ordinal
        );

        return Task.CompletedTask;
    }

    public async Task UpsertPosts(List<Models.Post.Post> posts)
    {
        if (posts.Count == 0)
            return;

        Dictionary<string, Models.Post.Post> cache = await GetCache() ?? [];
        Dictionary<string, PostMetaRow> postMeta = await GetPostMetaCache();
        _postsCache ??= cache;

        foreach (Models.Post.Post post in posts)
        {
            if (string.IsNullOrWhiteSpace(post.Id))
                continue;

            Models.Post.Post clone = post.Clone();
            cache[clone.Id] = clone;
            postMeta[clone.Id] = new()
            {
                Id = clone.Id,
                Hash = ComputePostHash(clone),
                Deleted = clone.Deleted,
            };
        }

        _postMetaCache = postMeta;
    }

    private void MergeData(Models.Post.Post post, Models.Post.Post result, Change change)
    {
        result.Profile.UserName ??= post.Profile.UserName;
        result.Profile.Name ??= post.Profile.Name;
        result.Profile.BannerUrl ??= post.Profile.BannerUrl;
        result.Profile.ImageUrl ??= post.Profile.ImageUrl;
        result.Profile.Following ??= post.Profile.Following;

        if (post.Equals(result))
            return;

        change.Data = post.Clone();

        _logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            post.Id,
            change.UserId,
            result.Profile.UserName
        );

        Extensions.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old data",
            "new data",
            ((Models.Post.Data)post).Clone(),
            ((Models.Post.Data)result).Clone()
        );
    }

    private void MergeIndex(
        Models.Post.Post post,
        Models.Post.Post result,
        Change change,
        string origin
    )
    {
        if (!post.Index.TryGetValue(change.UserId, out Dictionary<string, IndexData>? oldUserIndex))
            return;

        if (!oldUserIndex.TryGetValue(origin, out IndexData? oldIndex))
            return;

        if (
            !result.Index.TryGetValue(
                change.UserId,
                out Dictionary<string, IndexData>? newUserIndex
            )
        )
            return;

        if (!newUserIndex.TryGetValue(origin, out IndexData? newIndex))
            return;

        if (
            oldIndex.Previous is null
            || oldIndex.Next is null
            || newIndex.Previous is null
            || newIndex.Next is null
            || oldIndex.Equals(newIndex)
        )
            return;

        change.Index = post.CloneIndex()[change.UserId];

        _logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            post.Id,
            change.UserId,
            result.Profile.UserName
        );

        Extensions.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old index",
            "new index",
            oldUserIndex,
            newUserIndex
        );
    }
}
