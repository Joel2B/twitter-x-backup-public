using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

    private async Task UpsertPostsInternal(List<Post> posts)
    {
        IReadOnlyList<Post> normalizedPosts = NormalizePosts(posts);

        if (normalizedPosts.Count == 0)
            return;

        PostsDbContext db = await GetDbContext();

        List<string> ids = normalizedPosts.Select(post => post.Id).ToList();
        DetachTrackedPostGraphByIds(db, ids);

        await using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            await DeletePostGraphByIds(db, ids);

            HashSet<string> profileIds = normalizedPosts
                .Select(post => post.Profile.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);

            Dictionary<string, PostProfileEntity> profilesById = await LoadProfilesByIds(
                db,
                [.. profileIds]
            );

            List<PostEntity> batch = [];

            foreach (Post post in normalizedPosts)
            {
                PostProfileEntity profileEntity = GetOrCreateProfileEntity(
                    db,
                    profilesById,
                    post.Profile
                );

                PostEntity created = ToEntity(post, profileEntity, _postChangeComputationService);
                batch.Add(created);

                if (batch.Count < SqlInChunkSize)
                    continue;

                db.Posts.AddRange(batch);
                batch.Clear();
            }

            if (batch.Count > 0)
                db.Posts.AddRange(batch);

            await UpsertHashMetaForPosts(db, normalizedPosts);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            db.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            string incomingIdsSample = JoinSample(ids);
            string trackedSummary = BuildTrackedGraphSummary(db, ids);

            _logger.LogError(
                ex,
                "sqlite upsert failed for post ids [{ids}] | {trackedSummary}",
                incomingIdsSample,
                trackedSummary
            );

            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task SaveInternal()
    {
        PostsDbContext db = await GetDbContext();

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
        else
        {
            _logger.LogInformation("sqlite save skipped: no tracked changes");
        }

        try
        {
            await Replicate();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "sqlite replicate failed; continuing without replica sync");
        }

        _logger.LogInformation("sqlite save complete: tracked changes persisted");
    }

    private Task PruneInternal()
    {
        _logger.LogInformation("running prune");
        _logger.LogInformation("prune: {value}", _config.Tasks.Prune);

        if (!_config.Tasks.Prune)
            return Task.CompletedTask;

        _logger.LogInformation("sqlite prune skipped: posts are retained as soft-delete.");
        return Task.CompletedTask;
    }

    private async Task ResetInternal(List<Post> posts)
    {
        PostsDbContext db = await GetDbContext();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            try
            {
                await db.PostChangeFields.ExecuteDeleteAsync();
                await db.PostChanges.ExecuteDeleteAsync();
                await db.PostMediaVariants.ExecuteDeleteAsync();
                await db.PostMedias.ExecuteDeleteAsync();
                await db.PostHashtags.ExecuteDeleteAsync();
                await db.PostIndexEntries.ExecuteDeleteAsync();
                await db.Posts.ExecuteDeleteAsync();
                await db.Profiles.ExecuteDeleteAsync();
                await db.PostHashMeta.ExecuteDeleteAsync();

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        IReadOnlyList<Post> normalized = NormalizePosts(posts);
        Dictionary<string, Post> normalizedPosts = normalized.ToDictionary(
            post => post.Id,
            StringComparer.Ordinal
        );

        if (normalizedPosts.Count == 0)
            return;

        HashSet<string> profileIds = normalizedPosts
            .Values.Select(post => post.Profile.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        Dictionary<string, PostProfileEntity> profilesById = await LoadProfilesByIds(
            db,
            [.. profileIds]
        );

        bool autoDetectOriginal = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            List<PostEntity> batch = [];

            foreach (Post post in normalizedPosts.Values)
            {
                PostProfileEntity profile = GetOrCreateProfileEntity(
                    db,
                    profilesById,
                    post.Profile
                );

                PostEntity entity = ToEntity(post, profile, _postChangeComputationService);
                batch.Add(entity);

                if (batch.Count < SqlInChunkSize)
                    continue;

                db.Posts.AddRange(batch);
                batch.Clear();
            }

            if (batch.Count > 0)
                db.Posts.AddRange(batch);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = autoDetectOriginal;
        }

        await UpsertHashMetaForPosts(db, normalizedPosts.Values);
    }

    private IReadOnlyList<Post> NormalizePosts(IReadOnlyCollection<Post> posts)
    {
        return PostSnapshotNormalizationAdapter.Normalize(_postSnapshotNormalizationService, posts);
    }
}
