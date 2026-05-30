using Backup.Infrastructure.Models.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Data.Posts;

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

        List<Post> posts = incoming.Where(post => !string.IsNullOrWhiteSpace(post.Id)).ToList();

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

        List<Post> resolved = [];

        foreach (Post result in posts)
        {
            if (!existingPosts.TryGetValue(result.Id, out Post? current))
            {
                resolved.Add(result.Clone());
                continue;
            }

            Post merged = result.Clone();
            Change change = new() { UserId = userId };
            MergeData(current, merged, change);

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

            if (options.Index)
            {
                if (!merged.Index.ContainsKey(userId))
                    merged.Index[userId] = [];

                merged.Index[userId][origin] = incomingIndexData ?? new();
                MergeIndex(current, merged, change, origin);
            }

            bool hasDataChange = change.Data is not null;
            bool hasIndexChange = options.Index && change.Index is not null;

            if (!hasDataChange && !hasIndexChange)
                continue;

            if (change.Data is not null || change.Index is not null)
                merged.Changes.Add(change);

            resolved.Add(merged);
        }

        if (resolved.Count == 0)
            return;

        await UpsertPosts(resolved);
    }

    private async Task UpsertPostsInternal(List<Post> posts)
    {
        if (posts.Count == 0)
            return;

        PostsDbContext db = await GetDbContext();

        List<Post> normalizedPosts = posts
            .Where(post => !string.IsNullOrWhiteSpace(post.Id))
            .GroupBy(post => post.Id, StringComparer.Ordinal)
            .Select(group => group.Last().Clone())
            .ToList();

        if (normalizedPosts.Count == 0)
            return;

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

                PostEntity created = ToEntity(post, profileEntity);
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

        Dictionary<string, Post> normalizedPosts = posts
            .Where(post => !string.IsNullOrWhiteSpace(post.Id))
            .GroupBy(post => post.Id, StringComparer.Ordinal)
            .Select(group => group.Last().Clone())
            .ToDictionary(post => post.Id, StringComparer.Ordinal);

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

                PostEntity entity = ToEntity(post, profile);
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

    private static PostProfileEntity GetOrCreateProfileEntity(
        PostsDbContext db,
        Dictionary<string, PostProfileEntity> profilesById,
        PostProfile profile
    )
    {
        string id = profile.Id;

        PostProfileEntity? tracked = db.Profiles.Local.FirstOrDefault(o => o.Id == id);

        if (tracked is not null)
        {
            UpdateProfileEntity(tracked, profile);
            profilesById[id] = tracked;
            return tracked;
        }

        if (profilesById.TryGetValue(id, out PostProfileEntity? existing))
        {
            if (db.Entry(existing).State == EntityState.Detached)
                db.Profiles.Attach(existing);

            UpdateProfileEntity(existing, profile);
            return existing;
        }

        PostProfileEntity created = ToProfileEntity(profile);
        profilesById[id] = created;
        db.Profiles.Add(created);

        return created;
    }

    private static void UpdateProfileEntity(PostProfileEntity entity, PostProfile profile)
    {
        entity.UserName = profile.UserName;
        entity.Name = profile.Name;
        entity.BannerUrl = profile.BannerUrl;
        entity.ImageUrl = profile.ImageUrl;
        entity.Following = profile.Following;
        entity.CountMedia = profile.Count?.Media;
    }

    private static PostEntity ToEntity(Post post, PostProfileEntity profile)
    {
        Dictionary<string, PostProfileEntity> profiles = new(StringComparer.Ordinal)
        {
            [profile.Id] = profile,
        };

        PostEntity entity = ToEntity(post, profiles);
        entity.Profile = profile;
        entity.ProfileId = profile.Id;
        return entity;
    }

    private static async Task<Dictionary<string, PostProfileEntity>> LoadProfilesByIds(
        PostsDbContext db,
        IReadOnlyCollection<string> ids
    )
    {
        if (ids.Count == 0)
            return new Dictionary<string, PostProfileEntity>(StringComparer.Ordinal);

        Dictionary<string, PostProfileEntity> result = new(StringComparer.Ordinal);

        foreach (List<string> chunk in ChunkStrings(ids))
        {
            List<PostProfileEntity> rows = await db
                .Profiles.Where(profile => chunk.Contains(profile.Id))
                .ToListAsync();

            foreach (PostProfileEntity row in rows)
                result[row.Id] = row;
        }

        return result;
    }

    private static async Task UpsertHashMetaForPosts(PostsDbContext db, IEnumerable<Post> posts)
    {
        Dictionary<string, Post> normalized = posts
            .Where(post => !string.IsNullOrWhiteSpace(post.Id))
            .GroupBy(post => post.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToDictionary(post => post.Id, StringComparer.Ordinal);

        if (normalized.Count == 0)
            return;

        foreach (List<string> chunk in ChunkStrings(normalized.Keys))
        {
            Dictionary<string, PostHashMetaEntity> existing = await db
                .PostHashMeta.Where(row => chunk.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id, StringComparer.Ordinal);

            foreach (string id in chunk)
            {
                Post post = normalized[id];
                string hash = Backup.Infrastructure.Utils.PostHash.Compute(post);

                if (existing.TryGetValue(id, out PostHashMetaEntity? row))
                {
                    row.Hash = hash;
                    row.Deleted = post.Deleted;
                    continue;
                }

                db.PostHashMeta.Add(
                    new PostHashMetaEntity
                    {
                        Id = id,
                        Hash = hash,
                        Deleted = post.Deleted,
                    }
                );
            }
        }
    }

    private void MergeData(Post post, Post result, Change change)
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

        Backup.Infrastructure.Logging.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old data",
            "new data",
            post.Clone(),
            result.Clone()
        );
    }

    private void MergeIndex(Post post, Post result, Change change, string origin)
    {
        if (!post.Index.ContainsKey(change.UserId))
            post.Index[change.UserId] = [];

        if (!post.Index[change.UserId].TryGetValue(origin, out IndexData? indexPost))
        {
            indexPost = new();
            post.Index[change.UserId][origin] = indexPost;
        }

        IndexData indexResult = result.Index[change.UserId][origin];

        if (
            indexPost.Previous is null
            || indexPost.Next is null
            || indexResult.Previous is null
            || indexResult.Next is null
            || indexPost.Equals(indexResult)
        )
            return;

        change.Index = post.CloneIndex()[change.UserId];

        _logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            post.Id,
            change.UserId,
            result.Profile.UserName
        );

        Backup.Infrastructure.Logging.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old index",
            "new index",
            post.Index[change.UserId],
            result.Index[change.UserId]
        );
    }
}
