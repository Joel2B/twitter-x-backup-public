using Backup.Infrastructure.Posts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data;

public partial class SqlitePostData
{
    private async Task<PostsDbContext> GetDbContext()
    {
        if (_db is not null)
            return _db;

        string dbPath = GetDatabasePath();
        _db = CreateDbContext(dbPath);

        return _db;
    }

    private static IEnumerable<List<string>> ChunkStrings(
        IEnumerable<string> values,
        int size = SqlInChunkSize
    )
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        List<string> buffer = new(size);

        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            buffer.Add(value);

            if (buffer.Count < size)
                continue;

            yield return buffer;
            buffer = new List<string>(size);
        }

        if (buffer.Count > 0)
            yield return buffer;
    }

    private static IQueryable<PostEntity> IncludePostGraph(IQueryable<PostEntity> query) =>
        query
            .Include(o => o.Profile)
            .Include(o => o.Hashtags)
            .Include(o => o.Medias)
            .ThenInclude(o => o.Variants)
            .Include(o => o.Changes)
            .ThenInclude(o => o.Fields)
            .Include(o => o.IndexEntries);

    private static IQueryable<PostEntity> IncludePostMediaGraph(IQueryable<PostEntity> query) =>
        query
            .Include(o => o.Profile)
            .Include(o => o.Medias)
            .ThenInclude(o => o.Variants)
            .Include(o =>
                o.Changes.Where(change =>
                    change.ChangeType == "data_update" || change.ChangeType == "data_index_update"
                )
            )
            .ThenInclude(o => o.Fields);

    private static async Task<List<PostEntity>> LoadPostGraphByIds(
        PostsDbContext db,
        IReadOnlyCollection<string> ids
    )
    {
        if (ids.Count == 0)
            return [];

        List<PostEntity> result = [];

        foreach (List<string> chunk in ChunkStrings(ids))
        {
            IQueryable<PostEntity> query = IncludePostGraph(db.Posts)
                .AsSplitQuery()
                .Where(post => chunk.Contains(post.Id))
                .AsNoTracking();

            List<PostEntity> rows = await query.ToListAsync();

            if (rows.Count > 0)
                result.AddRange(rows);
        }

        return result;
    }

    private static async Task<Dictionary<string, bool>> LoadDeletedByIds(
        PostsDbContext db,
        IEnumerable<string> ids
    )
    {
        List<string> normalized = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
            return [];

        Dictionary<string, bool> result = new(StringComparer.Ordinal);

        foreach (List<string> chunk in ChunkStrings(normalized))
        {
            List<PostHashMetaEntity> rows = await db
                .PostHashMeta.AsNoTracking()
                .Where(row => chunk.Contains(row.Id))
                .ToListAsync();

            foreach (PostHashMetaEntity row in rows)
                result[row.Id] = row.Deleted;
        }

        return result;
    }

    private static bool GetDeleted(IReadOnlyDictionary<string, bool> deletedById, string id) =>
        deletedById.TryGetValue(id, out bool deleted) && deleted;

    private static MediaInput ToMediaInput(PostEntity entity, bool deleted) =>
        new()
        {
            Id = entity.Id,
            Profile = new()
            {
                Id = entity.Profile.Id,
                UserName = entity.Profile.UserName,
                Name = entity.Profile.Name,
                BannerUrl = entity.Profile.BannerUrl,
                ImageUrl = entity.Profile.ImageUrl,
                Following = entity.Profile.Following,
                Count = entity.Profile.CountMedia.HasValue
                    ? new PostCount { Media = entity.Profile.CountMedia }
                    : null,
            },
            Medias = entity
                .Medias.OrderBy(o => o.Ordinal)
                .Select(ToModel)
                .Select(media => media.Clone())
                .ToList(),
            Deleted = deleted,
        };

    private static MediaInput ToMediaInput(PostData data) =>
        new()
        {
            Id = data.Id,
            Profile = data.Profile.Clone(),
            Medias = data.Medias?.Select(media => media.Clone()).ToList(),
            Deleted = data.Deleted,
        };

    private static async Task DeletePostGraphByIds(
        PostsDbContext db,
        IReadOnlyCollection<string> ids
    )
    {
        if (ids.Count == 0)
            return;

        foreach (List<string> chunk in ChunkStrings(ids))
        {
            IQueryable<int> changeIds = db
                .PostChanges.Where(change => chunk.Contains(change.PostId))
                .Select(change => change.Id);

            IQueryable<int> mediaIds = db
                .PostMedias.Where(media => chunk.Contains(media.PostId))
                .Select(media => media.Id);

            await db
                .PostChangeFields.Where(field => changeIds.Contains(field.ChangeId))
                .ExecuteDeleteAsync();

            await db
                .PostMediaVariants.Where(variant => mediaIds.Contains(variant.MediaRefId))
                .ExecuteDeleteAsync();

            await db
                .PostChanges.Where(change => chunk.Contains(change.PostId))
                .ExecuteDeleteAsync();

            await db.PostMedias.Where(media => chunk.Contains(media.PostId)).ExecuteDeleteAsync();

            await db
                .PostHashtags.Where(hashtag => chunk.Contains(hashtag.PostId))
                .ExecuteDeleteAsync();

            await db
                .PostIndexEntries.Where(entry => chunk.Contains(entry.PostId))
                .ExecuteDeleteAsync();

            await db.Posts.Where(post => chunk.Contains(post.Id)).ExecuteDeleteAsync();
            await db.PostHashMeta.Where(row => chunk.Contains(row.Id)).ExecuteDeleteAsync();
        }
    }

}
