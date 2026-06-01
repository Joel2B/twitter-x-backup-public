using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
    private static void DetachTrackedPostGraphByIds(
        PostsDbContext db,
        IReadOnlyCollection<string> ids
    )
    {
        if (ids.Count == 0)
            return;

        HashSet<string> idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (idSet.Count == 0)
            return;

        HashSet<int> mediaIds = db
            .ChangeTracker.Entries<PostMediaEntity>()
            .Where(entry => idSet.Contains(entry.Entity.PostId))
            .Select(entry => entry.Entity.Id)
            .ToHashSet();

        if (mediaIds.Count > 0)
            DetachEntries<PostMediaVariantEntity>(
                db,
                entry => mediaIds.Contains(entry.Entity.MediaRefId)
            );

        HashSet<int> changeIds = db
            .ChangeTracker.Entries<PostChangeEntity>()
            .Where(entry => idSet.Contains(entry.Entity.PostId))
            .Select(entry => entry.Entity.Id)
            .ToHashSet();

        if (changeIds.Count > 0)
            DetachEntries<PostChangeFieldEntity>(
                db,
                entry => changeIds.Contains(entry.Entity.ChangeId)
            );

        DetachEntries<PostHashtagEntity>(db, entry => idSet.Contains(entry.Entity.PostId));
        DetachEntries<PostMediaEntity>(db, entry => idSet.Contains(entry.Entity.PostId));
        DetachEntries<PostIndexEntryEntity>(db, entry => idSet.Contains(entry.Entity.PostId));
        DetachEntries<PostChangeEntity>(db, entry => idSet.Contains(entry.Entity.PostId));
        DetachEntries<PostHashMetaEntity>(db, entry => idSet.Contains(entry.Entity.Id));
        DetachEntries<PostEntity>(db, entry => idSet.Contains(entry.Entity.Id));
    }

    private static void DetachEntries<TEntity>(
        PostsDbContext db,
        Func<EntityEntry<TEntity>, bool> predicate
    )
        where TEntity : class
    {
        foreach (
            EntityEntry<TEntity> entry in db
                .ChangeTracker.Entries<TEntity>()
                .Where(predicate)
                .ToList()
        )
            entry.State = EntityState.Detached;
    }

    private static string BuildTrackedGraphSummary(
        PostsDbContext db,
        IReadOnlyCollection<string> ids
    )
    {
        HashSet<string> idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (idSet.Count == 0)
            return "ids=0";

        List<string> trackedPostIds = db
            .ChangeTracker.Entries<PostEntity>()
            .Where(entry => idSet.Contains(entry.Entity.Id))
            .Select(entry => entry.Entity.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        int trackedHashtags = db
            .ChangeTracker.Entries<PostHashtagEntity>()
            .Count(entry => idSet.Contains(entry.Entity.PostId));

        List<PostMediaEntity> trackedMedias = db
            .ChangeTracker.Entries<PostMediaEntity>()
            .Where(entry => idSet.Contains(entry.Entity.PostId))
            .Select(entry => entry.Entity)
            .ToList();

        int trackedMediaCount = trackedMedias.Count;
        HashSet<int> trackedMediaIds = trackedMedias.Select(media => media.Id).ToHashSet();

        int trackedVariants = db
            .ChangeTracker.Entries<PostMediaVariantEntity>()
            .Count(entry => trackedMediaIds.Contains(entry.Entity.MediaRefId));

        List<PostChangeEntity> trackedChanges = db
            .ChangeTracker.Entries<PostChangeEntity>()
            .Where(entry => idSet.Contains(entry.Entity.PostId))
            .Select(entry => entry.Entity)
            .ToList();

        int trackedChangeCount = trackedChanges.Count;
        HashSet<int> trackedChangeIds = trackedChanges.Select(change => change.Id).ToHashSet();

        int trackedChangeFields = db
            .ChangeTracker.Entries<PostChangeFieldEntity>()
            .Count(entry => trackedChangeIds.Contains(entry.Entity.ChangeId));

        int trackedIndexEntries = db
            .ChangeTracker.Entries<PostIndexEntryEntity>()
            .Count(entry => idSet.Contains(entry.Entity.PostId));

        int trackedHashMeta = db
            .ChangeTracker.Entries<PostHashMetaEntity>()
            .Count(entry => idSet.Contains(entry.Entity.Id));

        int totalTracked = db.ChangeTracker.Entries().Count();

        return string.Join(
            ", ",
            [
                $"incomingIds={idSet.Count}",
                $"trackedPosts={trackedPostIds.Count}",
                $"trackedPostIds=[{JoinSample(trackedPostIds)}]",
                $"trackedHashtags={trackedHashtags}",
                $"trackedMedias={trackedMediaCount}",
                $"trackedVariants={trackedVariants}",
                $"trackedChanges={trackedChangeCount}",
                $"trackedChangeFields={trackedChangeFields}",
                $"trackedIndexEntries={trackedIndexEntries}",
                $"trackedHashMeta={trackedHashMeta}",
                $"trackedTotal={totalTracked}",
            ]
        );
    }

    private static string JoinSample(IEnumerable<string> values, int max = 20)
    {
        List<string> list = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(max)
            .ToList();

        return string.Join(",", list);
    }
}
