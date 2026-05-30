using Backup.Infrastructure.Posts.Models;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Posts.Data;

public partial class SqlitePostData
{
    public async Task<List<Post>?> GetAll()
    {
        PostsDbContext db = await GetDbContext();

        List<PostEntity> entities = await IncludePostGraph(db.Posts)
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        Dictionary<string, bool> deletedById = await LoadDeletedByIds(
            db,
            entities.Select(entity => entity.Id)
        );

        List<Post> posts = entities
            .Select(entity => ToModel(entity, GetDeleted(deletedById, entity.Id)))
            .ToList();

        return posts.Count == 0 ? null : posts;
    }

    public async Task<int> GetCount()
    {
        PostsDbContext db = await GetDbContext();
        return await db.Posts.CountAsync();
    }

    public async Task<PostStoreCounts> GetStoreCounts()
    {
        PostsDbContext db = await GetDbContext();

        return new PostStoreCounts
        {
            Posts = await db.Posts.CountAsync(),
            Profiles = await db.Profiles.CountAsync(),
            Hashtags = await db.PostHashtags.CountAsync(),
            Medias = await db.PostMedias.CountAsync(),
            MediaVariants = await db.PostMediaVariants.CountAsync(),
            IndexEntries = await db.PostIndexEntries.CountAsync(),
            Changes = await db.PostChanges.CountAsync(),
            ChangeFields = await db.PostChangeFields.CountAsync(),
            HashMeta = await db.PostHashMeta.CountAsync(),
        };
    }

    public async Task<List<MediaInput>?> GetMediaInputs()
    {
        PostsDbContext db = await GetDbContext();

        List<PostEntity> entities = await IncludePostMediaGraph(db.Posts)
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        if (entities.Count == 0)
            return null;

        Dictionary<string, bool> deletedById = await LoadDeletedByIds(
            db,
            entities.Select(entity => entity.Id)
        );

        List<MediaInput> current = entities
            .Select(entity => ToMediaInput(entity, GetDeleted(deletedById, entity.Id)))
            .ToList();

        List<MediaInput> history = [];

        foreach (PostEntity entity in entities)
        {
            if (entity.Changes.Count == 0)
                continue;

            Post post = ToModel(entity, GetDeleted(deletedById, entity.Id));

            history.AddRange(
                post.Changes.Where(change => change.Data is not null)
                    .Select(change => ToMediaInput(change.Data!))
            );
        }

        current.AddRange(history);
        return current;
    }

    public async Task<Dictionary<string, string>> GetHashesById()
    {
        PostsDbContext db = await GetDbContext();

        return await db
            .PostHashMeta.AsNoTracking()
            .ToDictionaryAsync(row => row.Id, row => row.Hash, StringComparer.Ordinal);
    }

    public async Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids)
    {
        if (ids.Count == 0)
            return [];

        HashSet<string> filter = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (filter.Count == 0)
            return [];

        PostsDbContext db = await GetDbContext();
        List<PostEntity> posts = await LoadPostGraphByIds(db, [.. filter]);

        Dictionary<string, bool> deletedById = await LoadDeletedByIds(
            db,
            posts.Select(post => post.Id)
        );

        return posts
            .Select(post => ToModel(post, GetDeleted(deletedById, post.Id)).Clone())
            .ToList();
    }

    public async Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    )
    {
        if (profileIds.Count == 0)
            return [];

        HashSet<string> filter = profileIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (filter.Count == 0)
            return [];

        PostsDbContext db = await GetDbContext();
        Dictionary<string, int> totals = new(StringComparer.Ordinal);

        foreach (List<string> chunk in ChunkStrings(filter))
        {
            List<KeyValuePair<string, int>> rows = await db
                .Posts.AsNoTracking()
                .Where(post => chunk.Contains(post.ProfileId))
                .GroupBy(post => post.ProfileId)
                .Select(group => new KeyValuePair<string, int>(group.Key, group.Count()))
                .ToListAsync();

            foreach (KeyValuePair<string, int> row in rows)
                totals[row.Key] = totals.TryGetValue(row.Key, out int count)
                    ? count + row.Value
                    : row.Value;
        }

        return totals;
    }
}
