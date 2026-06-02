using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;
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

        _logger.LogInformation(
            "add-posts: starting, user={userId}, origin={origin}, incoming={incomingCount}, index={index}",
            userId,
            origin,
            incoming.Count,
            options.Index
        );

        Dictionary<string, Post> posts = await GetCache() ?? [];
        Dictionary<string, PostMetaRow> postMeta = await GetPostMetaCache();
        _postsCache ??= posts;

        _logger.LogInformation(
            "add-posts: cache loaded, posts={postCount}, postMeta={metaCount}",
            posts.Count,
            postMeta.Count
        );

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
            _mutationCoordinator.BuildMergeMutations(
                userId,
                origin,
                incomingDomain,
                existingDomain,
                domainOptions
            );

        _logger.LogInformation(
            "add-posts: merge plan built, mutations={mutationCount}",
            mutations.Count
        );

        int inserted = 0;
        int updated = 0;
        int skipped = 0;
        int dataChanges = 0;
        int indexChanges = 0;

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
                inserted++;
                continue;
            }

            if (mutation.ShouldLogDataChange)
            {
                LogDataChange(current, merged, userId);
                dataChanges++;
            }

            if (mutation.ShouldLogIndexChange)
            {
                LogIndexChange(current, merged, userId, origin);
                indexChanges++;
            }

            if (!mutation.ShouldPersist)
            {
                skipped++;
                continue;
            }

            posts[mutation.Id] = merged;

            postMeta[mutation.Id] = new()
            {
                Id = mutation.Id,
                Hash = mutation.Hash,
                Deleted = mutation.Deleted,
            };
            updated++;
        }

        _postMetaCache = postMeta;

        _logger.LogInformation(
            "add-posts: completed, inserted={inserted}, updated={updated}, skipped={skipped}, dataChanges={dataChanges}, indexChanges={indexChanges}, finalPosts={finalPostCount}",
            inserted,
            updated,
            skipped,
            dataChanges,
            indexChanges,
            posts.Count
        );
    }

    public Task Reset(List<Post> posts)
    {
        IReadOnlyList<Post> normalized = NormalizePosts(posts);

        _logger.LogInformation(
            "reset: starting, incoming={incomingCount}, normalized={normalizedCount}",
            posts.Count,
            normalized.Count
        );

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

        _logger.LogInformation(
            "reset: completed, cache={postCount}, postMeta={metaCount}",
            _postsCache.Count,
            _postMetaCache.Count
        );

        return Task.CompletedTask;
    }

    public async Task UpsertPosts(List<Post> posts)
    {
        IReadOnlyList<Post> normalized = NormalizePosts(posts);

        _logger.LogInformation(
            "upsert-posts: starting, incoming={incomingCount}, normalized={normalizedCount}",
            posts.Count,
            normalized.Count
        );

        if (normalized.Count == 0)
        {
            _logger.LogInformation("upsert-posts: no normalized posts");
            return;
        }

        Dictionary<string, Post> cache = await GetCache() ?? [];
        Dictionary<string, PostMetaRow> postMeta = await GetPostMetaCache();
        _postsCache ??= cache;

        _logger.LogInformation(
            "upsert-posts: cache loaded, posts={postCount}, postMeta={metaCount}",
            cache.Count,
            postMeta.Count
        );

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

        _logger.LogInformation(
            "upsert-posts: completed, cache={postCount}, postMeta={metaCount}",
            cache.Count,
            postMeta.Count
        );
    }

    private IReadOnlyList<Post> NormalizePosts(IReadOnlyCollection<Post> posts)
    {
        return _mutationCoordinator.Normalize(posts);
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
