using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;

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

        IReadOnlyDictionary<string, Backup.Domain.Posts.Post> existingDomain = posts.ToDictionary(
            entry => entry.Key,
            entry => PostReplicationMapper.ToDomain(entry.Value),
            StringComparer.Ordinal
        );
        List<Backup.Domain.Posts.Post> incomingDomain = incoming
            .Select(PostReplicationMapper.ToDomain)
            .ToList();
        Backup.Domain.Posts.MergeOptions domainOptions = PostReplicationMapper.ToDomain(options);

        IReadOnlyList<Backup.Application.Posts.Models.PostStoreMergeMutation> mutations =
            _postStoreMergeMutationService.BuildMergeMutations(
                userId,
                origin,
                incomingDomain,
                existingDomain,
                domainOptions
            );

        foreach (Backup.Application.Posts.Models.PostStoreMergeMutation mutation in mutations)
        {
            Post merged = PostReplicationMapper.ToApp(mutation.MergedPost);
            posts.TryGetValue(mutation.Id, out Post? current);

            if (current is null)
            {
                Post clone = merged.Clone();
                posts[mutation.Id] = clone;

                postMeta[mutation.Id] = new()
                {
                    Id = mutation.Id,
                    Hash = mutation.Hash,
                    Deleted = mutation.Deleted,
                };
                continue;
            }

            if (mutation.ShouldLogDataChange)
                LogDataChange(current, merged, userId);

            if (mutation.ShouldLogIndexChange)
                LogIndexChange(current, merged, userId, origin);

            if (!mutation.ShouldPersist)
                continue;

            posts[mutation.Id] = merged;

            postMeta[mutation.Id] = new()
            {
                Id = mutation.Id,
                Hash = mutation.Hash,
                Deleted = mutation.Deleted,
            };
        }

        _postMetaCache = postMeta;
    }

    public Task Reset(List<Post> posts)
    {
        IReadOnlyList<Post> normalized = NormalizePosts(posts);

        _postsCache = normalized.ToDictionary(
            post => post.Id,
            post => post.Clone(),
            StringComparer.Ordinal
        );

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
