using Backup.Infrastructure.Models.Posts;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Posts.Data;

public partial class SqlitePostData
{
    private async Task<int> MarkDeletedExceptInternal(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    )
    {
        PostsDbContext db = await GetDbContext();

        HashSet<string> keep = keepPostIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        List<string> scopedIds = await db
            .PostIndexEntries.AsNoTracking()
            .Where(entry => entry.UserId == userId && entry.Origin == origin)
            .Select(entry => entry.PostId)
            .Distinct()
            .ToListAsync();

        if (scopedIds.Count == 0)
            return 0;

        if (keep.Count > 0)
            scopedIds = scopedIds.Where(id => !keep.Contains(id)).ToList();

        if (scopedIds.Count == 0)
            return 0;

        Dictionary<string, PostHashMetaEntity> metaById = new(StringComparer.Ordinal);

        foreach (List<string> chunk in ChunkStrings(scopedIds))
        {
            List<PostHashMetaEntity> rows = await db
                .PostHashMeta.Where(row => chunk.Contains(row.Id) && !row.Deleted)
                .ToListAsync();

            foreach (PostHashMetaEntity row in rows)
                metaById[row.Id] = row;
        }

        if (metaById.Count == 0)
            return 0;

        List<PostEntity> sourceEntities = await LoadPostGraphByIds(db, [.. metaById.Keys]);

        if (sourceEntities.Count == 0)
            return 0;

        int marked = 0;
        DateTime changeDate = DateTime.Now;

        foreach (PostEntity entity in sourceEntities)
        {
            if (!metaById.TryGetValue(entity.Id, out PostHashMetaEntity? meta))
                continue;

            if (meta.Deleted)
                continue;

            PostChangeEntity change = new()
            {
                PostId = entity.Id,
                UserId = userId,
                Date = changeDate,
                ChangeType = "data_update",
                Fields =
                [
                    new PostChangeFieldEntity
                    {
                        Field = ChangeFields.PostDeleted,
                        OldValueJson = SerializeJson(false),
                        NewValueJson = SerializeJson(true),
                    },
                ],
            };

            db.PostChanges.Add(change);
            meta.Deleted = true;
            meta.Hash = Backup.Infrastructure.Utils.PostHash.Compute(ToModel(entity, deleted: true));
            marked++;
        }

        return marked;
    }
}
