using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
    private async Task<int> MarkDeletedExceptInternal(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    )
    {
        PostsDbContext db = await GetDbContext();

        List<string> scopedIds = await db
            .PostIndexEntries.AsNoTracking()
            .Where(entry => entry.UserId == userId && entry.Origin == origin)
            .Select(entry => entry.PostId)
            .Distinct()
            .ToListAsync();

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

        List<Backup.Domain.Posts.Post> domainPosts = sourceEntities
            .Where(entity => metaById.ContainsKey(entity.Id))
            .Select(entity => ToModel(entity, metaById[entity.Id].Deleted))
            .Select(PostReplicationMapper.ToDomain)
            .ToList();
        Dictionary<string, bool> deletedById = metaById.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Deleted,
            StringComparer.Ordinal
        );
        IReadOnlySet<string> idsToDelete = _postSoftDeleteExecutionService.SelectIdsToMarkDeleted(
            userId,
            origin,
            keepPostIds,
            domainPosts,
            deletedById
        );

        if (idsToDelete.Count == 0)
            return 0;

        int marked = 0;
        DateTime changeDate = _dateTimeProvider.Now;

        foreach (PostEntity entity in sourceEntities)
        {
            if (!metaById.TryGetValue(entity.Id, out PostHashMetaEntity? meta))
                continue;

            if (meta.Deleted)
                continue;

            if (!idsToDelete.Contains(entity.Id))
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
            Backup.Domain.Posts.Post domainPost = PostReplicationMapper.ToDomain(
                ToModel(entity, deleted: true)
            );
            meta.Hash = _postHashingService.Compute(domainPost);
            marked++;
        }

        return marked;
    }
}
