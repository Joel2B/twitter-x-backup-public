using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Json;

public partial class LocalPostData
{
    public async Task AddPosts(
        string userId,
        string origin,
        List<Post> incoming,
        MergeOptions? options = null
    )
    {
        options ??= new();

        Dictionary<string, Post> posts = await GetCache() ?? [];
        Dictionary<string, PostMetaRow> postMeta = await GetPostMetaCache();
        _postsCache ??= posts;

        foreach (Post result in incoming)
        {
            if (!posts.TryGetValue(result.Id, out Post? post))
            {
                Post clone = result.Clone();
                posts[result.Id] = clone;

                postMeta[result.Id] = new()
                {
                    Id = result.Id,
                    Hash = ComputePostHash(clone),
                    Deleted = clone.Deleted,
                };
                continue;
            }

            Backup.Domain.Posts.Post currentDomain = PostReplicationMapper.ToDomain(post);
            Backup.Domain.Posts.Post incomingDomain = PostReplicationMapper.ToDomain(result);
            Backup.Domain.Posts.MergeOptions domainOptions = PostReplicationMapper.ToDomain(options);

            Backup.Application.Posts.Models.PostMergeOutcome merge = _postMergeService.Merge(
                userId,
                origin,
                currentDomain,
                incomingDomain,
                domainOptions
            );

            Post merged = PostReplicationMapper.ToApp(merge.MergedPost);

            if (merge.HasDataChange)
                LogDataChange(post, merged, userId);

            if (merge.HasIndexChange)
                LogIndexChange(post, merged, userId, origin);

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

    public Task Reset(List<Post> posts)
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

    public async Task UpsertPosts(List<Post> posts)
    {
        if (posts.Count == 0)
            return;

        Dictionary<string, Post> cache = await GetCache() ?? [];
        Dictionary<string, PostMetaRow> postMeta = await GetPostMetaCache();
        _postsCache ??= cache;

        foreach (Post post in posts)
        {
            if (string.IsNullOrWhiteSpace(post.Id))
                continue;

            Post clone = post.Clone();
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

    private void LogDataChange(Post current, Post merged, string userId)
    {
        _logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            current.Id,
            userId,
            merged.Profile.UserName
        );

        Backup.Infrastructure.Logging.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old data",
            "new data",
            ((PostData)current).Clone(),
            ((PostData)merged).Clone()
        );
    }

    private void LogIndexChange(Post current, Post merged, string userId, string origin)
    {
        if (!current.Index.TryGetValue(userId, out Dictionary<string, IndexData>? oldUserIndex))
            return;

        if (!oldUserIndex.ContainsKey(origin))
            return;

        if (!merged.Index.TryGetValue(userId, out Dictionary<string, IndexData>? newUserIndex))
            return;

        if (!newUserIndex.ContainsKey(origin))
            return;

        _logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            current.Id,
            userId,
            merged.Profile.UserName
        );

        Backup.Infrastructure.Logging.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old index",
            "new index",
            oldUserIndex,
            newUserIndex
        );
    }
}
