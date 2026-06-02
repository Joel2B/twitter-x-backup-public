using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
    private async Task AddPostsInternal(
        string userId,
        string origin,
        List<Post> incoming,
        MergeOptions options
    )
    {
        PostsDbContext db = await GetDbContext();

        List<Post> posts = NormalizePosts(incoming).ToList();

        if (posts.Count == 0)
            return;

        HashSet<string> ids = posts.Select(post => post.Id).ToHashSet(StringComparer.Ordinal);
        List<PostEntity> existingEntities = await LoadPostGraphByIds(db, [.. ids]);

        Dictionary<string, bool> deletedById = await LoadDeletedByIds(
            db,
            existingEntities.Select(post => post.Id)
        );

        Dictionary<string, Post> existingPosts = existingEntities.ToDictionary(
            post => post.Id,
            post => ToModel(post, GetDeleted(deletedById, post.Id)),
            StringComparer.Ordinal
        );

        IReadOnlyDictionary<string, Backup.Domain.Posts.Post> existingDomain =
            existingPosts.ToDictionary(
                entry => entry.Key,
                entry => PostReplicationMapper.ToDomain(entry.Value),
                StringComparer.Ordinal
            );
        List<Backup.Domain.Posts.Post> incomingDomain = posts
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

        List<Post> resolved = [];

        foreach (Backup.Application.Posts.Models.PostStoreMergeMutation mutation in mutations)
        {
            Post merged = PostReplicationMapper.ToApp(mutation.MergedPost);
            existingPosts.TryGetValue(mutation.Id, out Post? current);

            if (!mutation.ShouldPersist)
                continue;

            if (current is null || mutation.IsNew)
            {
                resolved.Add(merged);
                continue;
            }

            if (mutation.ShouldLogDataChange)
                LogDataChange(current, merged, userId);

            if (mutation.ShouldLogIndexChange)
                LogIndexChange(current, merged, userId);

            resolved.Add(merged);
        }

        if (resolved.Count == 0)
            return;

        await UpsertPosts(resolved);
    }

    private void LogDataChange(Post current, Post merged, string userId)
    {
        PostMergeDiagnosticsLogger.LogDataChange(
            _logger,
            current,
            merged,
            userId,
            current.Clone(),
            merged.Clone()
        );
    }

    private void LogIndexChange(Post current, Post merged, string userId)
    {
        PostMergeDiagnosticsLogger.TryLogIndexChange(_logger, current, merged, userId);
    }
}
