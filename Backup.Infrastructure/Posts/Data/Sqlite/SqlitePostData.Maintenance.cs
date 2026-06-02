using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
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
}
