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

        Dictionary<string, Backup.Domain.Posts.Post> existingDomain = posts.ToDictionary(
            entry => entry.Key,
            entry => PostReplicationMapper.ToDomain(entry.Value),
            StringComparer.Ordinal
        );

        List<Backup.Domain.Posts.Post> incomingDomain = incoming
            .Select(PostReplicationMapper.ToDomain)
            .ToList();

        Backup.Domain.Posts.MergeOptions domainOptions = PostReplicationMapper.ToDomain(options);

        IReadOnlyList<Backup.Application.Posts.Models.PostMergeResolutionItem> resolved =
            _postMergeResolutionService.Resolve(
                userId,
                origin,
                incomingDomain,
                existingDomain,
                domainOptions
            );

        foreach (Backup.Application.Posts.Models.PostMergeResolutionItem item in resolved)
        {
            Post merged = PostReplicationMapper.ToApp(item.MergedPost);

            if (!posts.TryGetValue(item.Id, out Post? current))
            {
                Post clone = merged.Clone();
                posts[item.Id] = clone;

                postMeta[item.Id] = new()
                {
                    Id = item.Id,
                    Hash = ComputePostHash(clone),
                    Deleted = clone.Deleted,
                };
                continue;
            }

            if (item.HasDataChange)
                LogDataChange(current, merged, userId);

            if (item.HasIndexChange)
                LogIndexChange(current, merged, userId, origin);

            posts[item.Id] = merged;

            postMeta[item.Id] = new()
            {
                Id = item.Id,
                Hash = ComputePostHash(merged),
                Deleted = merged.Deleted,
            };
        }

        _postMetaCache = postMeta;
    }

    public Task Reset(List<Post> posts)
    {
        IReadOnlyList<Post> normalized = NormalizePosts(posts);

        _postsCache = normalized.ToDictionary(post => post.Id, post => post.Clone(), StringComparer.Ordinal);

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
        IReadOnlyList<Post> normalized = NormalizePosts(posts);

        if (normalized.Count == 0)
            return;

        Dictionary<string, Post> cache = await GetCache() ?? [];
        Dictionary<string, PostMetaRow> postMeta = await GetPostMetaCache();
        _postsCache ??= cache;

        foreach (Post post in normalized)
        {
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

    private IReadOnlyList<Post> NormalizePosts(IReadOnlyCollection<Post> posts)
    {
        List<Backup.Domain.Posts.Post> domainPosts = posts.Select(PostReplicationMapper.ToDomain).ToList();
        IReadOnlyList<Backup.Domain.Posts.Post> normalized = _postSnapshotNormalizationService.Normalize(domainPosts);
        return normalized.Select(PostReplicationMapper.ToApp).ToList();
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
