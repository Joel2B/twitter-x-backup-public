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

        List<string> ids = normalizedPosts.Select(post => post.Id).ToList();
        DetachTrackedPostGraphByIds(db, ids);

        await using var tx = await db.Database.BeginTransactionAsync();

        try
        {
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
            await tx.CommitAsync();
            db.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
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
}
