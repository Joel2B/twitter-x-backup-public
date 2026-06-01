using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;

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

        IReadOnlyList<PostMergePlanDecision> decisions = PostMergePlanAdapter.BuildDecisions(
            _postMergeExecutionService,
            userId,
            origin,
            incoming,
            posts,
            options
        );

        foreach (PostMergePlanDecision item in decisions)
        {
            Post merged = item.Merged;
            Post? current = item.Current;

            if (current is null)
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

            if (item.ShouldLogDataChange)
                LogDataChange(current, merged, userId);

            if (item.ShouldLogIndexChange)
                LogIndexChange(current, merged, userId, origin);

            if (!item.ShouldPersist)
                continue;

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
        return PostSnapshotNormalizationAdapter.Normalize(_postSnapshotNormalizationService, posts);
    }

    private void LogDataChange(Post current, Post merged, string userId)
    {
        PostMergeDiagnosticsLogger.LogDataChange(
            _logger,
            current,
            merged,
            userId,
            ((PostData)current).Clone(),
            ((PostData)merged).Clone()
        );
    }

    private void LogIndexChange(Post current, Post merged, string userId, string origin)
    {
        PostMergeDiagnosticsLogger.TryLogIndexChange(_logger, current, merged, userId, origin);
    }
}
