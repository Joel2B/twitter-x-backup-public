using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Postgres;

public partial class PostgresPostData
{
    private async Task UpsertPostsInternal(List<Post> posts)
    {
        IReadOnlyList<Post> normalizedPosts = NormalizePosts(posts);

        if (normalizedPosts.Count == 0)
            return;

        PostsDbContext db = await GetDbContext();

        await using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            normalizedPosts = await ExpandSharedProfileUpdates(db, normalizedPosts);
            List<string> ids = normalizedPosts.Select(post => post.Id).ToList();
            DetachTrackedPostGraphByIds(db, ids);

            await DeletePostGraphByIds(db, ids);

            Dictionary<string, PostProfileEntity> profilesById = await LoadProfilesForPosts(
                db,
                normalizedPosts
            );

            bool autoDetectOriginal = db.ChangeTracker.AutoDetectChangesEnabled;
            db.ChangeTracker.AutoDetectChangesEnabled = false;

            try
            {
                List<PostEntity> batch = [];

                foreach (Post post in normalizedPosts)
                    BufferPostEntity(db, profilesById, batch, post);

                FlushPostBatch(db, batch);
            }
            finally
            {
                db.ChangeTracker.AutoDetectChangesEnabled = autoDetectOriginal;
            }

            await UpsertHashMetaForPosts(db, normalizedPosts);
            await db.SaveChangesAsync();
            await DeleteOrphanProfiles(db);
            await tx.CommitAsync();
            db.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            List<string> ids = normalizedPosts.Select(post => post.Id).ToList();
            string incomingIdsSample = JoinSample(ids);
            string trackedSummary = BuildTrackedGraphSummary(db, ids);

            _logger.LogError(
                ex,
                "postgres upsert failed for post ids [{ids}] | {trackedSummary}",
                incomingIdsSample,
                trackedSummary
            );

            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<IReadOnlyList<Post>> ExpandSharedProfileUpdates(
        PostsDbContext db,
        IReadOnlyList<Post> posts
    )
    {
        Dictionary<string, PostProfile> profiles = new(StringComparer.Ordinal);

        foreach (Post post in posts)
            profiles[post.Profile.Id] = post.Profile.Clone();

        List<Post> normalized = posts
            .Select(post =>
            {
                Post clone = post.Clone();
                clone.Profile = profiles[post.Profile.Id].Clone();
                return clone;
            })
            .ToList();

        Dictionary<string, PostProfileEntity> existing = await LoadProfilesByIds(
            db,
            [.. profiles.Keys]
        );
        HashSet<string> changedProfileIds = profiles
            .Where(entry =>
                existing.TryGetValue(entry.Key, out PostProfileEntity? stored)
                && !ProfileEquals(stored, entry.Value)
            )
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (changedProfileIds.Count == 0)
            return normalized;

        HashSet<string> incomingIds = normalized
            .Select(post => post.Id)
            .ToHashSet(StringComparer.Ordinal);
        List<PostEntity> siblingEntities = await IncludePostGraph(db.Posts)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(post =>
                changedProfileIds.Contains(post.ProfileId) && !incomingIds.Contains(post.Id)
            )
            .ToListAsync();
        Dictionary<string, bool> deletedById = await LoadDeletedByIds(
            db,
            siblingEntities.Select(entity => entity.Id)
        );

        foreach (PostEntity entity in siblingEntities)
        {
            Post sibling = ToModel(entity, GetDeleted(deletedById, entity.Id));
            sibling.Profile = profiles[entity.ProfileId].Clone();
            normalized.Add(sibling);
        }

        return normalized;
    }

    private static bool ProfileEquals(PostProfileEntity entity, PostProfile profile) =>
        entity.UserName == profile.UserName
        && entity.Name == profile.Name
        && entity.BannerUrl == profile.BannerUrl
        && entity.ImageUrl == profile.ImageUrl
        && entity.Following == profile.Following
        && entity.CountMedia == profile.Count?.Media;
}
